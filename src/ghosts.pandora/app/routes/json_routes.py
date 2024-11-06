from fastapi import APIRouter, Response
import random
import logging
from faker import Faker
from utils.ollama import generate_document_with_ollama
from utils.helper import generate_random_name
from config.config import OLLAMA_ENABLED

router = APIRouter()
fake = Faker()
logger = logging.getLogger("app_logger")

model = "llama3.2"


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
            prompt = f"Produce a valid JSON array with at least {num_rows} number of rows and at least 10 columns based on a random subject. No additional text or formatting."
            logger.info("Sending request to Ollama with prompt: %s", prompt)

            body = generate_document_with_ollama(prompt, model)

            if body:  # Check if body is successfully generated
                logger.debug("Generated JSON data from Ollama: %s", body)
                return Response(
                    content=body,
                    media_type="application/json",
                    headers={"Content-Disposition": "inline; filename=data.json"},
                )
            else:
                logger.warning(
                    "Ollama did not return valid data, falling back to Faker."
                )

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
            "Content-Disposition": f"inline; filename={path if not None else generate_random_name()}.json"
        },
    )
