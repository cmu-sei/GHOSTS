import random

from app_logging import setup_logger
from config.config import IMAGE_MODEL, OLLAMA_ENABLED
from fastapi import APIRouter, Response
from utils.helper import generate_image_response
from utils.ollama import generate_document_with_ollama
from utils.text2image import generate_image_with_diffusers

# Setup logger
logger = setup_logger(__name__)

# Initialize FastAPI router
router = APIRouter()


# Helper function to generate and return an image
def generate_image_from_prompt(prompt: str, request_type: str) -> Response:
    """Generate an image based on the prompt and return it as a Response."""
    logger.info(f"Generating image with prompt: {prompt}")

    # Generate the image using the diffusers model
    image_data = generate_image_with_diffusers(prompt, image_format=request_type)
    if image_data:
        # Ensure we seek to the start of the image_bytes before returning it
        image_data.seek(0)
        return Response(content=image_data.read(), media_type=f"image/{request_type}")

    logger.warning(
        "Failed to generate image, falling back to default image generation."
    )
    # Fallback to default image generation if it fails
    return generate_image_response(request_type)


# Helper function to generate an enhanced prompt using Ollama
def generate_enhanced_prompt(input_text: str) -> str:
    """Generate a detailed, creative, and visually rich prompt using Ollama."""
    stripped_input_text = input_text.split(".")[0]
    image_prompt = f"Based on the following text '{stripped_input_text}', create a detailed and prompt for generating a high-quality images. The generated prompt should provide more context to the subject given and artistic elements such as lighting, composition, mood, and style. No more than 50 words. Return only the prompt no other commentary"

    # Generate the detailed prompt from Ollama
    ollama_image_prompt = generate_document_with_ollama(image_prompt, IMAGE_MODEL)
    if ollama_image_prompt:
        logger.info(
            f"Enhanced image prompt generated: {ollama_image_prompt[:50]}..."
        )  # Log the first 50 characters for brevity
        return ollama_image_prompt[:50]  # Return only the first 50 characters
    else:
        logger.warning(
            "Failed to generate enhanced prompt, falling back to basic input."
        )
        return input_text  # Return the input text as-is if Ollama fails


# Endpoint to return a random image
@router.get("/i", tags=["Image"])
@router.post("/i", tags=["Image"])
@router.get("/img", tags=["Image"])
@router.post("/img", tags=["Image"])
@router.get("/images", tags=["Image"])
@router.post("/images", tags=["Image"])
def return_random_image() -> Response:
    """Generate and return a random image if enabled."""
    request_type = random.choice(["jpg", "png", "gif"])

    if OLLAMA_ENABLED:
        prompt = "A random image that would make someone laugh"
        logger.info(f"Requesting image prompt generation with prompt: {prompt}")

        # Generate the enhanced prompt using Ollama
        enhanced_prompt = generate_enhanced_prompt(prompt)

        # Generate and return the image based on the enhanced prompt
        return generate_image_from_prompt(enhanced_prompt, request_type)

    # Fallback to default image generation if Ollama is not enabled or fails
    return generate_image_response(request_type)


# Endpoint to return an image based on a dynamic path
@router.get("/i/{path:path}", tags=["Image"])
@router.post("/i/{path:path}", tags=["Image"])
@router.get("/img/{path:path}", tags=["Image"])
@router.post("/img/{path:path}", tags=["Image"])
@router.get("/images/{path:path}", tags=["Image"])
@router.post("/images/{path:path}", tags=["Image"])
def return_image(path: str) -> Response:
    """Generate and return an image based on the request path, if enabled."""
    request_type = (
        path.split(".")[-1].upper()
        if "." in path
        else random.choice(["JPEG", "PNG", "GIF"])
    )

    # Validate the requested image type
    if request_type not in ["JPEG", "PNG", "GIF"]:
        logger.warning(
            f"Invalid image type requested: {request_type}. Defaulting to JPEG."
        )
        request_type = "JPEG"

    logger.info(f"Received request to generate image of type: {request_type}")

    if OLLAMA_ENABLED:
        logger.info(f"Requesting image prompt generation with prompt: {path}")

        # Generate the enhanced prompt using Ollama
        # enhanced_prompt = generate_enhanced_prompt(path)

        # Generate and return the image based on the enhanced prompt
        return generate_image_from_prompt(path, request_type)

    # Fallback to default image generation if Ollama is not enabled or fails
    return generate_image_response(request_type)
