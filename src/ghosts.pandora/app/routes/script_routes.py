import os

from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, SCRIPT_MODEL
from faker import Faker
from fastapi import APIRouter, HTTPException, Response
from utils.ollama import generate_document_with_ollama

fake = Faker()
router = APIRouter()
logger = setup_logger(__name__)


@router.get("/script/{filename:path}", tags=["Web"])
@router.post("/script/{filename:path}", tags=["Web"])
@router.get("/script", tags=["Web"])
@router.post("/script", tags=["Web"])
def return_script(filename: str = None) -> Response:
    """
    Generate a script based on the filename extension.

    Args:
        filename (str): Filename provided in the route, including extension.

    Returns:
        Response: A response containing the generated script.
    """
    if not filename:
        filename = "script.js"
        logger.info(f"No filename provided. Defaulting to '{filename}'")
    # Determine script type from file extension
    _, ext = os.path.splitext(filename)
    script_type = "python" if ext == ".py" else "javascript"

    logger.info(f"Generating a {script_type} script for filename: {filename}")

    if OLLAMA_ENABLED:
        prompt = f"Give me a {script_type} script without any quotes around it. Just the code."
        logger.info(f"Sending request to Ollama model with prompt: {prompt}")

        try:
            generated_script = generate_document_with_ollama(prompt, SCRIPT_MODEL)

            if generated_script:
                media_type = (
                    "text/javascript"
                    if script_type == "javascript"
                    else "text/x-python"
                )
                extension = "js" if script_type == "javascript" else "py"

                filename = f"{fake.word()}_{fake.uuid4()}.{extension}"  # Generate random filename with correct extension
                logger.info(f"Script generated successfully using Ollama: {filename}")

                response = Response(
                    content=generated_script.encode("utf8"), media_type=media_type
                )
                response.headers["Content-Disposition"] = (
                    f"attachment; filename={filename}"
                )
                return response
            else:
                logger.warning(
                    "Ollama failed to generate script. Falling back to default generation."
                )

        except Exception as e:
            logger.error(f"Error generating script with Ollama: {e}", exc_info=True)
            logger.info("Falling back to Faker for script generation.")

    try:
        if script_type == "javascript":
            body = f"console.log('{fake.word()}, {fake.date()}');"
            media_type = "text/javascript"
            extension = "js"
        else:
            body = f"""
# Simple Python script
import datetime

def main():
print('{fake.word()}, {fake.date()}')

if __name__ == "__main__":
    main()
                    """
            media_type = "text/x-python"
            extension = "py"

        filename = f"{fake.word()}_{fake.uuid4()}.{extension}"  # Generate random filename with correct extension
        logger.info(f"Script generated: {filename}")

        response = Response(content=body.encode("utf8"), media_type=media_type)
        response.headers["Content-Disposition"] = f"attachment; filename={filename}"
        return response

    except Exception as e:
        logger.error(f"Error during fallback script generation: {e}", exc_info=True)
        raise HTTPException(
            status_code=500, detail="An error occurred while generating the script."
        )
