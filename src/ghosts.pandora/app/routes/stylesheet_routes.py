from fastapi import APIRouter, Response
import configparser
import random
import os
import app_logging
from utils.ollama import generate_document_with_ollama
from config.config import OLLAMA_ENABLED

logger = app_logging.setup_logger("app_logger")

router = APIRouter()
config = configparser.ConfigParser()
config.read(os.path.join("app", "config", "app.config"))

ollama_enabled = True  # Set Ollama enabled status
model = "llama3.2"  # Specify the model you want to use with Ollama


@router.get("/stylesheet", tags=["Web"])
@router.post("/stylesheet", tags=["Web"])
def return_stylesheet() -> Response:
    """Return a CSS stylesheet with random font styles and additional properties."""

    if OLLAMA_ENABLED:
        # Prepare a prompt for generating CSS styles
        prompt = "Provide a plain CSS stylesheet with random font styles, colours and sizes without any enclosing quotes"
        logger.info("Sending request to Ollama model with prompt: %s", prompt)

        generated_css = generate_document_with_ollama(prompt, model)

        if generated_css:
            logger.info("CSS stylesheet generated successfully using Ollama.")
            response = Response(
                content=generated_css.encode("utf8"), media_type="text/css"
            )
            response.headers["Content-Disposition"] = "inline; filename=style.css"
            return response
        else:
            logger.warning(
                "Ollama failed to generate CSS. Falling back to default generation."
            )

    # Fallback to random CSS generation if Ollama is disabled or fails
    # Get fonts from configuration
    fonts = config.get("css", "fonts_array").split(",")

    # Randomly select fonts and styles
    selected_font = random.choice(fonts)
    heading_font = random.choice(fonts)
    width = random.randint(65, 100)
    background_color = "#{:06x}".format(random.randint(0, 0xFFFFFF))  # Random hex color

    # Generate CSS body
    body = (
        f"* {{font-family: {selected_font};}} "
        f"h1 {{font-family: {heading_font};}} "
        f"body {{width: {width}%; background-color: {background_color};}} "
        f"p {{font-size: {random.randint(12, 20)}px;}}"
    )

    # Log the generated styles
    logger.info(
        f"Generated CSS with font '{selected_font}' for body and '{heading_font}' for h1."
    )
    logger.info(
        f"Body width set to {width}%, background color set to {background_color}."
    )

    response = Response(content=body.encode("utf8"), media_type="text/css")
    response.headers["Content-Disposition"] = "inline; filename=style.css"

    # Log the response status
    logger.info("CSS stylesheet generated successfully.")

    return response
