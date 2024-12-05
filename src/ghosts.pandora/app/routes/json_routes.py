import json
import random

from app_logging import setup_logger
from config.config import JSON_MODEL, OLLAMA_ENABLED
from faker import Faker
from fastapi import APIRouter, Response
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama

router = APIRouter()
fake = Faker()
logger = setup_logger(__name__)


@router.get("/api", tags=["Data Structures"])
@router.post("/api", tags=["Data Structures"])
@router.get("/json", tags=["Data Structures"])
@router.post("/json", tags=["Data Structures"])
@router.get("/api/{path:path}", tags=["Data Structures"])
@router.post("/api/{path:path}", tags=["Data Structures"])
@router.get("/json/{path:path}", tags=["Data Structures"])
@router.post("/json/{path:path}", tags=["Data Structures"])
def return_json(path: str = "") -> Response:
    """Return a JSON response with random data for any request starting with /api or /json."""
    num_rows = random.randint(1, 100)

    if path:  # If path is not empty, log it
        logger.info(
            "Generating JSON with %d rows for path: /api/%s or /json/%s",
            num_rows,
            path,
            path,
        )
    else:  # If path is empty, log it differently
        logger.info("Generating JSON with %d rows for path: /api or /json", num_rows)

    # Attempt to generate JSON using Ollama
    if OLLAMA_ENABLED:
        try:
            prompt = f"Produce a valid JSON array with at least {num_rows} rows and at least 10 columns based on a random subject. No additional text or formatting."
            logger.info("Sending request to Ollama with prompt: %s", prompt)

            body = generate_document_with_ollama(prompt, JSON_MODEL)

            # Validate if the generated body is valid JSON
            try:
                json_data = json.loads(body)  # Attempt to parse the JSON
                body = json.dumps(json_data)  # Re-format the JSON to ensure it's valid
                logger.debug("Generated valid JSON data from Ollama: %s", body)
                return Response(
                    content=body,
                    media_type="application/json",
                    headers={"Content-Disposition": "inline; filename=data.json"},
                )
            except json.JSONDecodeError:
                logger.warning("Ollama returned invalid JSON, falling back to Faker.")

        except Exception as e:
            logger.error("Error while calling Ollama: %s", str(e))
            logger.warning("Falling back to Faker for JSON generation.")

    # Fallback to Faker for JSON generation
    body = fake.json(
        data_columns={"Candidates": ["name", "name", "name"]},
        num_rows=num_rows,
    )
    logger.debug("Generated JSON data from Faker: %s", body)

    return Response(
        content=body,
        media_type="application/json",
        headers={
            "Content-Disposition": f"inline; filename={path if path else generate_random_name()}.json"
        },
    )
