from fastapi import APIRouter, Depends, HTTPException
from utils.dependencies import decode_jwt, Request
from langchain_community.chat_models import ChatOllama
from handlers.filters import filter_llm_response
import app_logging
from typing import Any

# Create an APIRouter instance
router = APIRouter()
logger = app_logging.setup_logger(__name__)


@router.post("/social")
async def social(
    request: Request, username: str = Depends(decode_jwt)
) -> dict[str, Any]:
    """
    Process a social interaction request from the user.

    Args:
        request (Request): The incoming request containing query parameters.
        username (str): The username extracted from the JWT token.

    Returns:
        dict[str, Any]: A JSON response containing the filtered social interaction response.
    """
    query = request.query
    logger.info(f"Processing social request for user: {username} with query: {query}")

    # Initialize the ChatOllama model for social interaction
    llm = ChatOllama(model="social", temperature=0.9)

    try:
        # Log the query being sent to the LLM
        logger.debug(f"Social query: {query}")

        # Generate a response using the LLM
        llm_response = llm.invoke(query).content

        # Filter the response using the imported filter function
        filtered_response = filter_llm_response(llm_response)

        # Log the generated LLM response
        logger.debug(f"Social response for {username}: {filtered_response}")

        # Return the response in a JSON format
        return {"response": filtered_response}

    except Exception as e:
        logger.error(f"Error processing social for {username}: {str(e)}")
        raise HTTPException(status_code=500, detail="Internal Server Error")
