import os
import random
import configparser

from fastapi import APIRouter, Request, Response
from config.config import OLLAMA_ENABLED, STYLESHEET_MODEL
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager
from app_logging import setup_logger

router = APIRouter()
logger = setup_logger(__name__)

# Load font list from config
config = configparser.ConfigParser()
config.read(os.path.join("../", "app", "config", "app.config"))
font_list = config.get("css", "fonts_array").split(",")


def return_stylesheet(request: Request) -> Response:
    cm = ContentManager(default="style", extension="css")
    cm.resolve(request)
    media_type = "text/css"

    if cm.is_storing():
        if cached := cm.load():
            logger.info(f"Returning cached stylesheet: {cm.file_name}")
            return Response(
                content=cached,
                media_type=media_type,
                headers={"Content-Disposition": f"inline; filename={cm.file_name}"},
            )

    # Try Ollama
    if OLLAMA_ENABLED:
        prompt = "Provide a plain CSS stylesheet with random font styles, colours and sizes without any enclosing quotes"
        logger.info(f"Ollama prompt: {prompt}")

        try:
            generated = generate_document_with_ollama(prompt, STYLESHEET_MODEL)
            if generated:
                logger.info("CSS generated via Ollama")
                if cm.is_storing():
                    cm.save(generated.encode("utf-8"))

                return Response(
                    content=generated.encode("utf-8"),
                    media_type=media_type,
                    headers={"Content-Disposition": f"inline; filename={cm.file_name}"},
                )
        except Exception as e:
            logger.error(f"Ollama error: {e}", exc_info=True)

    # Fallback CSS generation
    font_body = random.choice(font_list)
    font_heading = random.choice(font_list)
    width = random.randint(65, 100)
    bg_color = "#{:06x}".format(random.randint(0, 0xFFFFFF))
    font_size = random.randint(12, 20)

    body = (
        f"* {{ font-family: {font_body}; }} "
        f"h1 {{ font-family: {font_heading}; }} "
        f"body {{ width: {width}%; background-color: {bg_color}; }} "
        f"p {{ font-size: {font_size}px; }}"
    )

    logger.info(
        f"Generated fallback CSS with fonts: {font_body}, {font_heading}, "
        f"width: {width}%, background: {bg_color}, size: {font_size}px"
    )

    if cm.is_storing():
        cm.save(body.encode("utf-8"))

    return Response(
        content=body.encode("utf-8"),
        media_type=media_type,
        headers={"Content-Disposition": f"inline; filename={cm.file_name}"},
    )


# Register routes
ROUTES = ["/css", "/styles", "/stylesheet"]
for route in ROUTES:
    router.add_api_route(route, return_stylesheet, methods=["GET", "POST"], tags=["Web"])
