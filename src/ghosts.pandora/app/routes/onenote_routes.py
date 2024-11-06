from fastapi import APIRouter, Response
from faker import Faker
import random
import app_logging
from utils.helper import generate_random_name
import os

router = APIRouter()
fake = Faker()


logger = app_logging.setup_logger("app_logger")


@router.get("/onenote", tags=["Documents"])
@router.post("/onenote", tags=["Documents"])
@router.get("/onenote/{file_name}", tags=["Documents"])
@router.post("/onenote/{file_name}", tags=["Documents"])
def return_onenote(file_name: str = None) -> Response:
    """Return a OneNote file with random binary content."""
    if file_name is None:
        file_name = generate_random_name(".one")
        logger.info("Generated random file name: %s", file_name)
    else:
        # Ensure the file name has a .one extension
        base_name, ext = os.path.splitext(file_name)
        if ext.lower() != ".one":
            file_name = f"{base_name}.one"
            logger.warning("No valid extension provided; changed to: %s", file_name)

    # Generate random binary content
    binary_content = fake.binary(length=random.randint(1000, 300000))
    logger.debug("Generated binary content of length: %d bytes", len(binary_content))

    # Create the response
    response = Response(content=binary_content, media_type="application/onenote")
    response.headers["Content-Disposition"] = f"attachment; filename={file_name}"
    logger.info("Serving OneNote file: %s", file_name)

    return response
