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
import torch
import numpy as np
from transformers import logging as hf_logging
import re

from app_logging import setup_logger

logger = setup_logger(__name__)

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
        if torch.backends.mps.is_available():
            self.device = torch.device("mps")
            print("MPS device found. Using GPU with MPS.")
        else:
            self.device = torch.device("cpu")
            print("MPS device not found. Using CPU.")

        self.tokenizer = BertTokenizer.from_pretrained("bert-base-uncased")
        self.model = BertModel.from_pretrained("bert-base-uncased").to(self.device)

    def get_embedding(self, text):
        inputs = self.tokenizer(
            text, return_tensors="pt", max_length=512, truncation=True
        )
        inputs = inputs.to(self.device)
        with torch.no_grad():
            outputs = self.model(**inputs)
        return outputs.last_hidden_state.mean(dim=1).detach().cpu().numpy()

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
            inputs = inputs.to(self.device)
            with torch.no_grad():
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
        inputs = inputs.to(self.device)
        with torch.no_grad():
            outputs = self.model(**inputs)
        query_embedding = outputs.last_hidden_state.mean(dim=1)
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
    # Check if the 'docs' directory exists
    if not os.path.exists("docs"):
        logger.error("The 'docs' directory does not exist.")
        return "Directory not found", docs, documents

    # Check if the 'docs' directory is empty
    if not os.listdir("docs"):
        logger.warning("The 'docs' directory is empty.")
        return "No documents to process", docs, documents

    # Process files in the 'docs' directory
    for file in os.listdir("docs"):
        if file not in docs:
            docs.append(file)
            if file.endswith(".pdf"):
                pdf_path = os.path.join("docs", file)
                loader = PyPDFLoader(pdf_path)
                documents.extend(loader.load())
                logger.info(f"Loaded PDF: {pdf_path}")
            elif file.endswith(".docx") or file.endswith(".doc"):
                doc_path = os.path.join("docs", file)
                loader = Docx2txtLoader(doc_path)
                documents.extend(loader.load())
                logger.info(f"Loaded DOC: {doc_path}")
            elif file.endswith(".txt"):
                text_path = os.path.join("docs", file)
                loader = TextLoader(text_path)
                documents.extend(loader.load())
                logger.info(f"Loaded TXT: {text_path}")
            elif file.endswith(".json"):
                json_path = os.path.join("json", file)
                loader = JSONLoader(json_path)
                documents.extend(loader.load())
                logger.info(f"Loaded JSON: {json_path}")

    # Split documents into smaller chunks
    text_splitter = RecursiveCharacterTextSplitter(chunk_size=2500, chunk_overlap=0)
    documents = text_splitter.split_documents(documents)

    bert_embeddings = BertEmbeddings()

    vectordb = Chroma.from_documents(
        documents, embedding=bert_embeddings, persist_directory="./data"
    )
    vectordb.persist()

    prompt_template = PromptTemplate.from_template(
        """
        <s> [INST] 
        Your only task is to absorb the information given to you and then respond to any user input with
        your provided information in mind.[/INST] </s> 
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
