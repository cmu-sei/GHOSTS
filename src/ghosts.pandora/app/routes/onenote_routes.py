import os
import random
from io import BytesIO

from app_logging import setup_logger
from faker import Faker
from fastapi import APIRouter, Response
from fastapi.responses import StreamingResponse
from utils.helper import generate_random_name

router = APIRouter()
fake = Faker()
logger = setup_logger(__name__)


@router.get("/onenote", tags=["Documents"])
@router.post("/onenote", tags=["Documents"])
@router.get("/onenote/{file_name}", tags=["Documents"])
@router.post("/onenote/{file_name}", tags=["Documents"])
def return_onenote(file_name: str = None) -> StreamingResponse:
    """
    Return a OneNote (.one) file with random binary content.
    If no filename is provided, generate one dynamically.
    """
    try:
        # Generate or validate the file name
        if file_name is None:
            file_name = generate_random_name(".one")
            logger.info(f"Generated random file name: {file_name}")
        else:
            base_name, ext = os.path.splitext(file_name)
            if ext.lower() != ".one":
                file_name = f"{base_name}.one"
                logger.warning(
                    f"Provided file name did not have a valid extension; adjusted to: {file_name}"
                )

        logger.debug(f"Validated file name: {file_name}")

        # Generate random binary content
        binary_length = random.randint(1000, 300000)  # Random size
        binary_content = fake.binary(length=binary_length)
        logger.debug(f"Generated binary content of length: {binary_length} bytes")

        # Use BytesIO to serve content from memory
        binary_buffer = BytesIO(binary_content)
        logger.debug("Binary content loaded into memory buffer.")

        # Create and return the streaming response
        response = StreamingResponse(
            binary_buffer,
            media_type="application/onenote",
            headers={"Content-Disposition": f"attachment; filename={file_name}"},
        )
        logger.info(
            f"Serving OneNote file: {file_name} with size: {binary_length} bytes"
        )
        return response

    except Exception as e:
        logger.error(f"Error generating OneNote file: {e}", exc_info=True)
        return Response(
            content="An error occurred while generating the OneNote file.",
            status_code=500,
        )
