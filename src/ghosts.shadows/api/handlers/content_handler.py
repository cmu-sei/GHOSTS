import os

from langchain.chains import ConversationalRetrievalChain
from langchain.text_splitter import RecursiveCharacterTextSplitter
from langchain.prompts import PromptTemplate

from langchain_community.document_loaders import PyPDFLoader
from langchain_community.document_loaders import Docx2txtLoader
from langchain_community.document_loaders import TextLoader
from langchain_community.document_loaders import JSONLoader
from langchain_community.vectorstores import Chroma

from langchain_ollama import ChatOllama
from transformers import BertModel, BertTokenizer
import numpy as np
from transformers import logging as hf_logging
import re

hf_logging.set_verbosity_error()
np.set_printoptions(
    edgeitems=3,
    infstr="inf",
    linewidth=75,
    nanstr="nan",
    precision=8,
    suppress=False,
    threshold=6,
    formatter=None,
)
os.environ["PYTHONHTTPSVERIFY"] = "0"


class BertEmbeddings:
    def __init__(self, model_name="bert-base-uncased", cache_dir="./data/bert"):
        self.tokenizer = BertTokenizer.from_pretrained("bert-base-uncased")
        self.model = BertModel.from_pretrained("bert-base-uncased")

    def get_embedding(self, text):
        inputs = self.tokenizer(
            text, return_tensors="pt", max_length=512, truncation=True
        )
        outputs = self.model(**inputs)
        return outputs.last_hidden_state.mean(dim=1).detach().numpy()

    def embed_documents(self, documents):
        embeddings = []
        for doc in documents:
            inputs = self.tokenizer(
                doc,
                return_tensors="pt",
                max_length=512,
                truncation=True,
                padding="max_length",
            )
            outputs = self.model(**inputs)
            doc_embedding = outputs.last_hidden_state.mean(dim=1)
            doc_embedding_list = (
                doc_embedding[0].detach().cpu().numpy().tolist()
            )  # Convert to list
            embeddings.append(doc_embedding_list)
        return embeddings

    def embed_query(self, query):
        inputs = self.tokenizer(
            query,
            return_tensors="pt",
            max_length=512,
            truncation=True,
            padding="max_length",
        )
        outputs = self.model(**inputs)
        query_embedding = outputs.last_hidden_state.mean(dim=1)
        # Convert to list
        return query_embedding[0].detach().cpu().numpy().tolist()


def filter_llm_response(content):
    content_sentences = re.split("([\:|\.|!|\?])", content)
    content_sentences = [
        (content_sentences[i] + content_sentences[i + 1])
        for i in range(0, int(len(content_sentences) / 2), 2)
    ]
    for i, sentence in enumerate(content_sentences):
        if re.match(r"\b\w+,\s*", sentence):
            sentence = re.sub(r"\b\w+,\s*", "", sentence)
            if sentence:
                sentence = sentence[0].upper() + sentence[1:]

        if re.match(r"^\s*\w+!", sentence):
            sentence = re.sub(r"^\w+!", "", sentence)

        if re.match(r"^\s*\w+\s\w+!", sentence):
            sentence = re.sub(r"^\w+\s\w+!", "", sentence)

        if re.match(
            r"^\s*.*\b(here is|here are|here\'s a|here\'s an|here\'s the|here\'s)\b.*$",
            sentence,
            flags=re.IGNORECASE,
        ):
            sentence = re.sub(
                r"^\s*.*\b(here is|here are|here\'s a|here\'s an|here\'s the|here\'s)\b.*$",
                "",
                sentence,
                flags=re.IGNORECASE,
            )

        if re.match(
            r"^\s*.*\b(a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you|)\b.*$",
            sentence,
            flags=re.IGNORECASE,
        ):
            sentence = re.sub(
                r"^\s*.*\b(a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you)\b.*$",
                "",
                sentence,
                flags=re.IGNORECASE,
            )

        if re.match(
            r"^\s*.*\b(hypothetical|may contain|based on the context provided|based on the provided context)\b.*$",
            sentence,
            flags=re.IGNORECASE,
        ):
            sentence = re.sub(
                r"^\s*.*\b(hypothetical|may contain|based on the context provided|based on the provided context)\b.*$",
                "",
                sentence,
                flags=re.IGNORECASE,
            )

        emoji_pattern = re.compile(
            "["
            "\U0001f600-\U0001f64f"  # emoticons
            "\U0001f300-\U0001f5ff"  # symbols & pictographs
            "\U0001f680-\U0001f6ff"  # transport & map symbols
            "\U0001f1e0-\U0001f1ff"  # flags (iOS)
            "]+",
            flags=re.UNICODE,
        )
        sentence = emoji_pattern.sub(r"", sentence)  # no emoji
        content_sentences[i] = sentence

    content = " ".join(content_sentences)
    return content.strip()


def capitalize_first_word(s):
    words = s.split()
    if words:
        words[0] = words[0].capitalize()
        return " ".join(words)
    return s


def main(query, docs, documents):
    # documents = []
    for file in os.listdir("docs"):
        if file not in docs:
            docs.append(file)
            if file.endswith(".pdf"):
                pdf_path = "./docs/" + file
                loader = PyPDFLoader(pdf_path)
                documents.extend(loader.load())
                print(f"Loaded pdf {pdf_path}")
            elif file.endswith(".docx") or file.endswith(".doc"):
                doc_path = "./docs/" + file
                loader = Docx2txtLoader(doc_path)
                documents.extend(loader.load())
                print(f"Loaded doc {doc_path}")
            elif file.endswith(".txt"):
                text_path = "./docs/" + file
                loader = TextLoader(text_path)
                documents.extend(loader.load())
                print(f"Loaded txt {text_path}")
            elif file.endswith(".json"):
                json_path = "./json/" + file
                loader = JSONLoader(json_path)
                documents.extend(loader.load())
                print(f"Loaded json {json_path}")

    text_splitter = RecursiveCharacterTextSplitter(chunk_size=250, chunk_overlap=0)
    documents = text_splitter.split_documents(documents)

    # vectordb = Chroma.from_documents(documents, embedding=FastEmbedEmbeddings(), persist_directory="./data")
    vectordb = Chroma.from_documents(
        documents, embedding=BertEmbeddings(), persist_directory="./data"
    )
    vectordb.persist()

    prompt_template = PromptTemplate.from_template(
        """
        <s> [INST] 
        You are DocuGen, specialized in creating realistic and detailed document content tailored to the needs of various professionals. 
        Use the following pieces of context to answer the question.
        Focus on understanding the context and requirements of each request to generate content that is both relevant and informative. 
        Consider the role, industry, and purpose of the document while maintaining a professional and coherent tone. 
        Be adaptable, providing structured and concise content that directly addresses the user's query.
        NEVER, NEVER talk about yourself, DocuGen or your company.
        Don't talk about the thing you wrote, just stick to the topic. [/INST] </s> 
        [INST] Prompt: {question} 
        Context: {context} 
        Answer: [/INST]
        """
    )

    pdf_qa = ConversationalRetrievalChain.from_llm(
        ChatOllama(model="mistral", temperature=0.9),
        vectordb.as_retriever(search_kwargs={"k": 3}),
        return_source_documents=True,
        verbose=False,
        combine_docs_chain_kwargs={"prompt": prompt_template},
    )

    chat_history = []
    result = pdf_qa.invoke({"question": query, "chat_history": chat_history})
    filtered_response = filter_llm_response(result["answer"])
    chat_history.append((query, result["answer"]))
    return filtered_response, docs, documents
