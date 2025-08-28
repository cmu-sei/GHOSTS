import json
import random

from fastapi import APIRouter, Request, Response
from faker import Faker

from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, JSON_MODEL
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager

router = APIRouter()
fake = Faker()
logger = setup_logger(__name__)


def return_json(request: Request) -> Response:
    cm = ContentManager(default="index", extension="json")
    cm.resolve(request)
    num_rows = random.randint(1, 100)

    logger.info(f"Generating JSON with {num_rows} rows for path: {cm.rel_path}")

    if cm.is_storing():
        if content := cm.load():
            return Response(
                content=content,
                media_type="application/json",
                headers={"Content-Disposition": f"inline; filename={cm.file_name}"},
            )

    # Try Ollama
    if OLLAMA_ENABLED:
        prompt = (
            f"Produce a valid JSON array with at least {num_rows} rows and at least "
            f"10 columns based on a random subject. No additional text or formatting."
        )
        logger.info(f"Ollama prompt: {prompt}")
        try:
            body = generate_document_with_ollama(prompt, JSON_MODEL)
            json_data = json.loads(body)
            body = json.dumps(json_data)
        except Exception as e:
            logger.warning(f"Ollama failed or returned invalid JSON: {e}")
            body = None

    # Fallback to Faker
    if not body:
        logger.info("Falling back to Faker for JSON generation.")
        body = fake.json(
            data_columns={"Candidates": ["name", "name", "name"]},
            num_rows=num_rows,
        )

    if cm.is_storing():
        cm.save(body)

    return Response(
        content=body,
        media_type="application/json",
        headers={"Content-Disposition": f"inline; filename={cm.file_name}"},
    )


# Register routes
ROUTES = ["/api", "/json"]
for route in ROUTES:
    router.add_api_route(route, return_json, methods=["GET", "POST"], tags=["Data Structures"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_json, methods=["GET", "POST"], tags=["Data Structures"])
