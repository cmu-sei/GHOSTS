from fastapi import APIRouter, Depends, HTTPException
from handlers.content_handler import (
    main as content_main,
)  # Adjust the import to your content handler
from utils.dependencies import decode_jwt, Request
from langchain_community.chat_models import ChatOllama
import app_logging

# Create an APIRouter instance
router = APIRouter()
logger = app_logging.setup_logger(__name__)

# Global lists to store content documents
content_docs: list = []
content_documents: list = []


@router.post("/content")  # Set the route to /content
async def content(request: Request, username: str = Depends(decode_jwt)) -> dict:
    """
    Process a content request from the user.

    Args:
        request (Request): The incoming request containing query parameters.
        username (str): The username extracted from the JWT token.

    Returns:
        dict: A JSON response containing the LLM's content response.
    """
    global content_docs, content_documents
    query = request.query

    # Log the incoming content request
    logger.info(f"Processing content request for user: {username} with query: {query}")

    # Initialize the ChatOllama model for content generation
    llm = ChatOllama(model="web_content", temperature=0.9)

    try:
        # Call the content handler's main function and get the response
        response, content_docs, content_documents = content_main(
            query, content_docs, content_documents
        )

        # Generate a response using the LLM
        llm_response = llm.invoke(query).content

        # Log the generated LLM response
        logger.debug(f"Content response for {username}: {llm_response}")

        # Return the response in a JSON format
        return {"response": llm_response}

    except Exception as e:
        logger.error(f"Error processing content for {username}: {str(e)}")
        raise HTTPException(status_code=500, detail="Internal Server Error")
