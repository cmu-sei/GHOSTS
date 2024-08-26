from flask import Flask, request, jsonify, render_template, send_file
from langchain.chains import ConversationalRetrievalChain
from langchain.memory import ConversationBufferMemory
from langchain.text_splitter import CharacterTextSplitter
from langchain.prompts import PromptTemplate

from langchain_community.document_loaders import PyPDFLoader
from langchain_community.document_loaders import Docx2txtLoader
from langchain_community.document_loaders import TextLoader
from langchain_community.vectorstores import Chroma

from langchain_community.chat_models import ChatOllama
from langchain_community.embeddings import FastEmbedEmbeddings

from transformers import BertModel, BertTokenizer
from handlers import content_handler, activities_handler, img_handler, lessons_handler

from jose import jwt, JWTError
import re
import csv
import os
import logging

# Initialize the logger
logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

SECRET_KEY = "Tartans1"
ALGORITHM = "HS256"

docs = []
documents = []

def filter(content):
    content_sentences = re.split(r'([:|.!?])', content)
    content_sentences = [(content_sentences[i] + content_sentences[i+1]) for i in range(0, len(content_sentences) - 1, 2)]
    for i, sentence in enumerate(content_sentences):
        if re.match(r'^[^:]*:', sentence):
            sentence = re.sub(r'^[^:]*:', '', sentence) 

        if re.match(r'\b\w+,\s*', sentence):
            sentence = re.sub(r'\b\w+,\s*', '', sentence)
            if sentence:
                sentence = sentence[0].upper() + sentence[1:]

        if re.match(r'^\s*\w+!', sentence):
            sentence = re.sub(r'^\w+!', '', sentence)

        if re.match(r'^\s*\w+\s\w+!', sentence):
            sentence = re.sub(r'^\w+\s\w+!', '', sentence)

        if re.match(r"^\s*.*\b(here is|here are|here's a|here's an|here's the|here's)\b.*$", sentence, flags = re.IGNORECASE):
            sentence = re.sub(r"^\s*.*\b(here is|here are|here's a|here's an|here's the|here's)\b.*$", "", sentence, flags = re.IGNORECASE)

        if re.match(r"^\s*.*\b(excel|URL|a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you|to optimize this tweet|provide one or two relevant hashtags for the tweet)\b.*$", sentence, flags = re.IGNORECASE):
            sentence = re.sub(r"^\s*.*\b(excel|URL|a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you|to optimize this tweet|provide one or two relevant hashtags for the tweet)\b.*$", "", sentence, flags=re.IGNORECASE)
        
        if re.match(r"^\s*.*\b(demonstration purposes|random|based on the context|randomly|just examples|hypothetical|may contain|based on the information|based on the provided context)\b.*$", sentence, flags = re.IGNORECASE):
            sentence = re.sub(r"^\s*.*\b(demonstration purposes|random|based on the context|randomly|just examples|hypothetical|may contain|based on the information|based on the provided context)\b.*$", "", sentence, flags = re.IGNORECASE)
        
        # Regex to remove any email addresses
        sentence = re.sub(r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "", sentence)
    
        emoji_pattern = re.compile("["
            u"\U0001F600-\U0001F64F"  # emoticons
            u"\U0001F300-\U0001F5FF"  # symbols & pictographs
            u"\U0001F680-\U0001F6FF"  # transport & map symbols
            u"\U0001F1E0-\U0001F1FF"  # flags (iOS)
                            "]+", flags=re.UNICODE)
        sentence = emoji_pattern.sub(r'', sentence) # no emoji
        content_sentences[i] = sentence

    content = " ".join(content_sentences)
    return content.strip()

api_app = Flask(__name__)

def decode_jwt(token: str):
    try:
        payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
        username: str = payload.get("sub")
        if not username:
            raise ValueError("Invalid token payload")
        return username
    except JWTError as e:
        logger.error(f"JWT Error: {str(e)}")
        return None

@api_app.before_request
def log_request_info():
    token = request.headers.get('Authorization')
    if token and token.startswith("Bearer "):
        token = token.split("Bearer ")[1]
        username = decode_jwt(token)
        if username:
            logger.debug('Incoming request: %s %s %s', request.method, request.url, request.data)
            logger.debug(f'User: {username}, Token: {token}')
        else:
            logger.debug('Invalid Token')
    else:
        logger.debug('No token provided')

@api_app.after_request
def log_response_info(response):
    content_type = response.headers.get('Content-Type', '')
    if 'image' not in content_type and 'binary' not in content_type:
        logger.debug('Outgoing response: %s', response.status)
        print("-------------------------------------------------------------------------------------------")
    else:
        logger.debug('Outgoing response (binary content): %s', response.status)
        print("-------------------------------------------------------------------------------------------")
    return response

@api_app.route('/', methods=['GET', 'POST'])
def home():
    return render_template('index.html')

@api_app.route('/assets/ghosts-shadows.png', methods=['GET'])
def image():
    return send_file("assets/ghosts-shadows.png", mimetype='image/png') 

@api_app.route('/activity', methods=['POST'])
def activity():
    global docs
    global documents
    logger.info("Processing activity request")
    llm = ChatOllama(model="activity", temperature=0.9)
    data = request.json
    query = data.get('query')
    logger.debug("Activity query: %s", query)
    response, docs, documents = activities_handler.main(query, docs, documents)
    response = llm.invoke(query).content
    logger.debug("Activity response: %s", response)
    return jsonify(response)

@api_app.route('/chat', methods=['POST'])
def chat():
    logger.debug(f"Connecting to ollama at {ollama_host} using chat model")
    llm = ChatOllama(base_url=ollama_host, model="chat", temperature=0.9) # , base_url="http://0.0.0.0:11434"
    data = request.json
    query = data.get('query')
    response = llm.invoke(query)
    logger.debug("Chat response: %s", response.content)
    return jsonify(response.content)

@api_app.route('/content', methods=['POST'])
def content():
    global docs
    global documents
    response = ""
    while len(response) == 0:
        web_llm = ChatOllama(base_url=ollama_host, model='web_content', temperature=0.9)
        img_llm = ChatOllama(base_url=ollama_host, model='img_content', temperature=0.9)
        excel_llm = ChatOllama(base_url=ollama_host, model='excel_content', temperature=0.9)

        data = request.json
        query = data.get('query')
        logger.debug("Content query: %s", query)
        if '.docx' in query or '.pdf' in query:
            response, docs, documents = content_handler.main(query, docs, documents)
        elif '.png' in query:
            prompt = img_llm.invoke(query).content
            prompt = filter(prompt)
            img_handler.main(prompt)
            logger.debug("Image handler response sent")
            return send_file("/handlers/my_image.jpg", mimetype = 'image/png')
        elif '.xls' in query:
            excel = excel_llm.invoke(query).content
            excel = filter(excel)
            response = excel.split('\n')
            with open ('excel.csv', 'w', newline = '') as csvfile:
                writer = csv.writer(csvfile, delimiter = ' ', quotechar = '|', quoting = csv.QUOTE_MINIMAL)
                for r in response:
                    if len(r) != 0:
                        r_list = r.split('| ')
                        r_list = r_list[1:len(r_list)-1]
                        r_list = [word.strip() for word in r_list]
                        writer.writerow(r_list)
            logger.debug("Excel handler response sent")
            return send_file("excel.csv", mimetype = 'text/csv')
        elif 'http' in query:
            response = web_llm.invoke(query).content
            response = filter(response)
        else:
            response, docs, documents = content_handler.main(query, docs, documents)
    logger.debug("Content response: %s", response)
    return jsonify(response)

@api_app.route('/social', methods=['POST'])
def social():
    llm = ChatOllama(base_url=ollama_host, model="social", temperature=0.9)
    data = request.json
    query = data.get('query')
    logger.debug("Social query: %s", query)
    response = llm.invoke(query).content
    response = filter(response)
    logger.debug("Social response: %s", response)
    return jsonify(response)

@api_app.route('/lessons', methods=['POST'])
def lessons():
    global docs
    global documents
    data = request.json
    query = data.get('query')
    logger.debug("Lessons query: %s", query)
    response, docs, documents = lessons_handler.main(query, docs, documents)
    response = filter(response)
    logger.debug("Lessons response: %s", response)
    return jsonify(response)

if __name__ == '__main__':
    api_app.logger.setLevel(logging.DEBUG)

    api_app.logger.info("""

            ('-. .-.               .-')    .-') _     .-')    
            ( OO )  /              ( OO ). (  OO) )   ( OO ).  
  ,----.    ,--. ,--. .-'),-----. (_)---\_)/     '._ (_)---\_) 
 '  .-./-') |  | |  |( OO'  .-.  '/    _ | |'--...__)/    _ |  
 |  |_( O- )|   .|  |/   |  | |  |\  :` `. '--.  .--'\  :` `.  
 |  | .--, \|       |\_) |  |\|  | '..`''.)   |  |    '..`''.) 
(|  | '. (_/|  .-.  |  \ |  | |  |.-._)   \   |  |   .-._)   \ 
 |  '--'  | |  | |  |   `'  '-'  '\       /   |  |   \       / 
  `------'  `--' `--'     `-----'  `-----'    `--'    `-----'  

""")
    api_app.logger.info("GHOSTS SHADOWS coming online...")

    ollama_host = os.getenv("GHOSTS_OLLAMA_URL", "http://localhost:11434")
    api_app.run(host='0.0.0.0', debug=True, port=5900)
