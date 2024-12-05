from io import BytesIO

from app_logging import setup_logger
from faker import Faker
from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from utils.helper import generate_random_name

router = APIRouter()
fake = Faker()

logger = setup_logger(__name__)


@router.get("/iso", tags=["Files"])
@router.post("/iso", tags=["Files"])
@router.get("/iso/{file_name}", tags=["Files"])
@router.post("/iso/{file_name}", tags=["Files"])
def return_iso(file_name: str = None) -> StreamingResponse:
    """Return an ISO file containing random data."""

    if file_name is None:
        file_name = generate_random_name(".iso")
    elif not file_name.endswith(".iso"):
        file_name += ".iso"  # Add .iso extension if not present

    logger.info(f"Generating ISO file: {file_name}")

    # Create a simple ISO file structure in memory
    iso_content = (
        f"This is a random ISO file with random content: {fake.paragraph()}".encode(
            "utf-8"
        )
    )

    # Create a BytesIO buffer to hold the ISO file
    iso_buffer = BytesIO(iso_content)

    # Seek to the beginning of the BytesIO buffer
    iso_buffer.seek(0)

    # Create the StreamingResponse
    response = StreamingResponse(
        iso_buffer,
        media_type="application/octet-stream",
        headers={"Content-Disposition": f"attachment; filename={file_name}"},
    )

    logger.info("ISO file generated successfully.")

    return response
