from fastapi import APIRouter, Depends, HTTPException
from handlers.lessons_handler import (
    main as lessons_main,
)  # Adjust the import to your lessons handler
from utils.dependencies import decode_jwt, Request
from langchain_community.chat_models import ChatOllama
import app_logging
from typing import Any

# Create an APIRouter instance
router = APIRouter()
logger = app_logging.setup_logger(__name__)

# Global lists to store lesson documents
lesson_docs: list = []
lesson_documents: list = []


@router.post("/lessons")  # Set the route to /lessons
async def lessons(
    request: Request, username: str = Depends(decode_jwt)
) -> dict[str, Any]:
    """
    Process a lessons request from the user.

    Args:
        request (Request): The incoming request containing query parameters.
        username (str): The username extracted from the JWT token.

    Returns:
        dict[str, Any]: A JSON response containing the LLM's lessons response.
    """
    global lesson_docs, lesson_documents
    query = request.query

    # Log the incoming lessons request
    logger.info(f"Processing lessons request for user: {username} with query: {query}")

    # Check if the query is empty
    if not query:
        logger.warning("Empty query received for lessons request.")
        raise HTTPException(status_code=400, detail="Query cannot be empty.")

    # Initialize the ChatOllama model for lesson generation
    try:
        llm = ChatOllama(model="lessons", temperature=0.9)
        logger.debug("ChatOllama model initialized successfully.")
    except Exception as e:
        logger.error(f"Failed to initialize ChatOllama model: {str(e)}")
        raise HTTPException(status_code=500, detail="Internal Server Error")

    try:
        # Call the lessons handler's main function and get the response
        logger.debug("Calling lessons_main with query.")
        response, lesson_docs, lesson_documents = lessons_main(
            query, lesson_docs, lesson_documents
        )
        logger.debug("Lessons_main function executed successfully.")

        # Generate a response using the LLM
        logger.debug("Invoking ChatOllama to generate response.")
        llm_response = llm.invoke(query).content

        # Log the generated LLM response
        logger.debug(f"Lessons response generated for {username}: {llm_response}")

        # Return the response in a JSON format
        return {"response": llm_response}

    except Exception as e:
        logger.error(f"Error processing lessons for {username}: {str(e)}")
        raise HTTPException(status_code=500, detail="Internal Server Error")
