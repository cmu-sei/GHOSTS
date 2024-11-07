from fastapi import APIRouter, Depends, HTTPException
from handlers.activities_handler import main as activity_main
from utils.dependencies import decode_jwt, Request
from handlers.filters import filter_llm_response
from langchain_ollama import ChatOllama
import app_logging

# Initialize an empty list for documents and docs
docs: list = []
documents: list = []

# Create an APIRouter instance
router = APIRouter()
logger = app_logging.setup_logger(__name__)


@router.post("/activity")
async def activity(request: Request, username: str = Depends(decode_jwt)) -> dict:
    """
    Process an activity request from the user.

    Args:
        request (Request): The incoming request containing query parameters.
        username (str): The username extracted from the JWT token.

    Returns:
        dict: A JSON response containing the filtered LLM response.
    """
    global docs
    global documents
    query = request.query

    # Log the incoming activity request
    logger.info(f"Processing activity request for user: {username} with query: {query}")

    # Initialize the ChatOllama model for activity
    llm = ChatOllama(model="activity", temperature=0.9)
    logger.debug("Initialized ChatOllama model with temperature: 0.9")

    try:
        # Log the state before calling the activity handler's main function
        logger.debug(f"About to call activity_main with query: {query}")

        # Call the activity handler's main function and get the response
        response, docs, documents = activity_main(query, docs, documents)

        # Log the number of documents loaded
        logger.debug(f"Documents loaded: {len(documents)}")
        logger.debug(f"Docs processed: {docs}")

        # Generate a response using the LLM
        llm_response = llm.invoke(query).content
        logger.debug(f"Raw LLM response for {username}: {llm_response}")

        # Process the response (if applicable)
        filtered_response = filter_llm_response(llm_response)
        logger.debug(f"Filtered LLM response for {username}: {filtered_response}")

        # Return the response in a JSON format
        return {"response": filtered_response}

    except Exception as e:
        logger.error(
            f"Error processing activity for {username}: {str(e)}", exc_info=True
        )
        raise HTTPException(status_code=500, detail="Internal Server Error")
