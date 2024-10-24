from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from faker import Faker
import app_logging
from utils.helper import (
    generate_random_name,
    generate_zip,
    generate_tar,
    generate_gz,
    create_response,
)

router = APIRouter()
fake = Faker()

logger = app_logging.setup_logger("app_logger")


@router.get("/zip", tags=["Archives"])
@router.post("/zip", tags=["Archives"])
@router.get("/zip/{file_name}", tags=["Archives"])
@router.post("/zip/{file_name}", tags=["Archives"])
def return_zip(file_name: str = None) -> StreamingResponse:
    """Return a ZIP file containing random binary data."""
    if file_name is None:
        file_name = generate_random_name(".zip")
    elif not file_name.endswith(".zip"):
        file_name += ".zip"

    logger.info(f"Generating ZIP file: {file_name}")
    buffer = generate_zip(file_name)
    return create_response(buffer, file_name, "application/zip")


@router.get("/tar", tags=["Archives"])
@router.post("/tar", tags=["Archives"])
@router.get("/tar/{file_name}", tags=["Archives"])
@router.post("/tar/{file_name}", tags=["Archives"])
def return_tar(file_name: str = None) -> StreamingResponse:
    """Return a TAR file containing random binary data."""
    if file_name is None:
        file_name = generate_random_name(".tar")
    elif not file_name.endswith(".tar"):
        file_name += ".tar"

    logger.info(f"Generating TAR file: {file_name}")
    buffer = generate_tar(file_name)
    return create_response(buffer, file_name, "application/x-tar")


@router.get("/gz", tags=["Archives"])
@router.post("/gz", tags=["Archives"])
@router.get("/gz/{file_name}", tags=["Archives"])
@router.post("/gz/{file_name}", tags=["Archives"])
def return_gz(file_name: str = None) -> StreamingResponse:
    """Return a GZ file containing random binary data."""
    if file_name is None:
        file_name = generate_random_name(".gz")
    elif not file_name.endswith(".gz"):
        file_name += ".gz"

    logger.info(f"Generating GZ file: {file_name}")
    buffer = generate_gz(file_name)
    return create_response(buffer, file_name, "application/gzip")
