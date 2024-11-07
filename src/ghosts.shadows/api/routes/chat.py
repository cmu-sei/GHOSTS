from fastapi import APIRouter, Depends, HTTPException
from utils.dependencies import decode_jwt, Request
from langchain_community.chat_models import ChatOllama
import app_logging

# Create an APIRouter instance
router = APIRouter()
logger = app_logging.setup_logger(__name__)

# Global lists to store chat documents
chat_docs: list = []
chat_documents: list = []


@router.post("/chat")
async def chat(request: Request, username: str = Depends(decode_jwt)) -> dict:
    """
    Process a chat request from the user.

    Args:
        request (Request): The incoming request containing query parameters.
        username (str): The username extracted from the JWT token.

    Returns:
        dict: A JSON response containing the LLM's chat response.
    """
    query = request.query
    logger.info(f"Processing chat request for user: {username} with query: {query}")

    # Initialize the ChatOllama model for chat
    llm = ChatOllama(model="chat", temperature=0.9)

    try:
        # Generate a response using the LLM
        llm_response = llm.invoke(query).content

        # Log the generated LLM response
        logger.debug(f"Chat response for {username}: {llm_response}")

        # Return the response in a JSON format
        return {"response": llm_response}

    except Exception as e:
        logger.error(f"Error processing chat for {username}: {str(e)}")
        raise HTTPException(status_code=500, detail="Internal Server Error")
