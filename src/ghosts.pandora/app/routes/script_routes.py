from fastapi import APIRouter, Response, HTTPException
from faker import Faker
import random
import app_logging
from utils.ollama import generate_document_with_ollama
from config.config import OLLAMA_ENABLED

fake = Faker()
router = APIRouter()
logger = app_logging.setup_logger("app_logger")

model = "llama3.2"  # Specify the model you want to use with Ollama
ollama_enabled = True  # Set Ollama enabled status


@router.get("/script/{filename:path}", tags=["Web"])
@router.post("/script/{filename:path}", tags=["Web"])
@router.get("/script", tags=["Web"])
@router.post("/script", tags=["Web"])
def return_script() -> Response:
    """Generate a random script in either JavaScript or Python."""

    # Randomly choose between JavaScript and Python
    script_type = random.choice(["javascript", "python"])
    logger.info(f"Generating a {script_type} script.")

    if OLLAMA_ENABLED:
        # Prepare a prompt for generating the script
        prompt = f"Give me a {script_type} script without any quotes around it. Just the code."
        logger.info("Sending request to Ollama model with prompt: %s", prompt)

        try:
            generated_script = generate_document_with_ollama(prompt, model)

            if generated_script:
                # Ensure correct media type and file extension based on script type
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
            # Fallback to Faker for script generation if an error occurs
            logger.info("Falling back to Faker for script generation.")

    # Fallback to Faker for script generation
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
