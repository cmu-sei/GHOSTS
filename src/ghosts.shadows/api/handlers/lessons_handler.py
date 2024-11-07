import os
import pickle
import numpy as np

from langchain.chains import ConversationalRetrievalChain
from langchain.text_splitter import RecursiveCharacterTextSplitter
from langchain.prompts import PromptTemplate

from langchain_community.document_loaders import PyPDFLoader
from langchain_community.document_loaders import Docx2txtLoader
from langchain_community.document_loaders import TextLoader
from langchain_community.document_loaders import JSONLoader
from langchain_community.vectorstores import Chroma
import torch
from transformers import BertModel, BertTokenizer

from langchain_ollama import ChatOllama
from transformers import logging as hf_logging
from handlers.filters import filter_llm_response

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


def capitalize_first_word(s):
    words = s.split()
    if words:
        words[0] = words[0].capitalize()
        return " ".join(words)
    return s


def main(query, docs, documents):
    vectordb_path = "../data/vectordb"
    vectorized_docs_path = "../data/vectorized_docs.pkl"

    if os.path.exists(vectorized_docs_path):
        vectordb = Chroma(
            persist_directory=vectordb_path, embedding_function=BertEmbeddings()
        )
        with open(vectorized_docs_path, "rb") as f:
            documents = pickle.load(f)
        print("Loaded vectorized documents and vector database from disk...")
    else:
        print("Vectorized documents not found. Vectorizing from disk...")
        for file in os.listdir("./docs"):
            if file not in docs:
                try:
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
                    else:
                        print(f"Document found that does not have a loader {file}")
                except Exception as e:
                    print(f"Error loading {file}: {e}")

        text_splitter = RecursiveCharacterTextSplitter(chunk_size=250, chunk_overlap=0)
        documents = text_splitter.split_documents(documents)

        vectordb = Chroma.from_documents(
            documents, embedding=BertEmbeddings(), persist_directory="../data"
        )
        vectordb.persist()

        with open(vectorized_docs_path, "wb") as f:
            pickle.dump(documents, f)
        print("Saved vectorized documents to disk.")

    prompt_template = PromptTemplate.from_template(
        """
        <s> [INST] 
        Embrace your role as a lesson plan or lesson module creator.
        Based on the information provided, you must produce appropriate sub-topics of things to be taught, knowledge checks and tasks that a user should be able to do for the topic, hands-on lab concepts, and similar for the lesson.
        Do NOT return anything but what I have asked. NO lengthy explanations into your reasoning please.
        [/INST] </s> 
        [INST] Prompt: {question} 
        Context: {context} 
        Answer: [/INST]
        """
    )

    pdf_qa = ConversationalRetrievalChain.from_llm(
        ChatOllama(model="lessons", temperature=0.9),
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
