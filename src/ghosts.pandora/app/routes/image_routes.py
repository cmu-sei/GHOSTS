import random
from fastapi import APIRouter, Request, Response

from app_logging import setup_logger
from config.config import IMAGE_MODEL, OLLAMA_ENABLED, STORE_RESULTS
from utils.ollama import generate_document_with_ollama
from utils.text2image import generate_image_with_diffusers
from utils.helper import generate_image_response
from utils.content_manager import ContentManager

router = APIRouter()
logger = setup_logger(__name__)


def generate_prompt(input_text: str) -> str:
    base = input_text.split(".")[0]
    prompt = (
        f"Based on the following text '{base}', create a detailed prompt for generating a high-quality image. "
        "Include artistic elements such as lighting, composition, mood, and style. No more than 50 words. "
        "Return only the prompt."
    )
    result = generate_document_with_ollama(prompt, IMAGE_MODEL)
    if result:
        logger.info(f"Enhanced image prompt: {result[:50]}...")
        return result[:50]
    logger.warning("Failed to generate enhanced prompt, using raw input.")
    return input_text


def generate_image(prompt: str, ext: str, cm: ContentManager) -> Response:
    logger.info(f"Generating image with prompt: {prompt}")

    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type=f"image/{ext}")

    image = generate_image_with_diffusers(prompt, image_format=ext)
    if image:
        image.seek(0)
        content = image.read()

        if cm.is_storing():
            cm.save(content)

        return Response(content=content, media_type=f"image/{ext}")

    logger.warning("Diffuser model failed. Falling back.")
    return generate_image_response(ext)


def resolve_image_type(path: str = None) -> str:
    ext = (path.split(".")[-1].lower() if path and "." in path else random.choice(["jpg", "png", "gif"]))
    if ext not in ["jpg", "png", "gif"]:
        logger.warning(f"Unsupported image type: {ext}. Defaulting to jpg.")
        return "jpg"
    return ext


def return_random_image(request: Request) -> Response:
    cm = ContentManager(default="random", extension="jpg" if not OLLAMA_ENABLED else "png")
    cm.resolve(request)

    ext = cm.extension
    prompt = "A random image that would make someone laugh"

    if OLLAMA_ENABLED:
        return generate_image(generate_prompt(prompt), ext, cm)
    return generate_image_response(ext)


def return_image(request: Request) -> Response:
    ext = resolve_image_type(request.url.path)
    cm = ContentManager(default="index", extension=ext)
    cm.resolve(request)

    if OLLAMA_ENABLED:
        return generate_image(generate_prompt(cm.full_path), ext, cm)
    return generate_image_response(ext)


ROUTES = ["/i", "/img", "/images"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_random_image, methods=["GET", "POST"], tags=["Image"])
    router.add_api_route(f"{route}/{{path:path}}", return_image, methods=["GET", "POST"], tags=["Image"])
