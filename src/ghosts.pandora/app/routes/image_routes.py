from fastapi import APIRouter, Response
import random
from faker import Faker
from utils.helper import generate_image_response
import app_logging

logger = app_logging.setup_logger("app_logger")

fake = Faker()

router = APIRouter()


# Define routes that respond to requests without a specific path
@router.get("/i", tags=["Image"])
@router.post("/i", tags=["Image"])
@router.get("/img", tags=["Image"])
@router.post("/img", tags=["Image"])
@router.get("/images", tags=["Image"])
@router.post("/images", tags=["Image"])
def return_random_image() -> Response:
    """Generate and return a random image without a specific path."""
    request_type = random.choice(["jpg", "png", "gif"])
    return generate_image_response(request_type)


# Define routes that respond to requests with a specific path
@router.get("/i/{path:path}", tags=["Image"])
@router.post("/i/{path:path}", tags=["Image"])
@router.get("/img/{path:path}", tags=["Image"])
@router.post("/img/{path:path}", tags=["Image"])
@router.get("/images/{path:path}", tags=["Image"])
@router.post("/images/{path:path}", tags=["Image"])
def return_image(path: str) -> Response:
    """Generate and return a random image based on the request path."""
    request_type = (
        path.split(".")[-1] if "." in path else random.choice(["jpg", "png", "gif"])
    )

    if request_type not in ["jpg", "png", "gif", "ico"]:
        logger.warning(
            f"Invalid image type requested: {request_type}. Defaulting to jpg."
        )
        request_type = "jpg"  # Default to jpg if invalid type is requested

    logger.info(f"Received request to generate image of type: {request_type}")
    return generate_image_response(request_type)
