import app_logging
from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from utils.helper import (create_response, generate_archive,
                          generate_random_name)

router = APIRouter()

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
    buffer = generate_archive(file_name, "zip")
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
    buffer = generate_archive(file_name, "tar")
    return create_response(buffer, file_name, "application/x-tar")
