import app_logging
from fastapi import APIRouter, Request
from fastapi.responses import StreamingResponse
from utils.helper import (create_response, generate_archive)
from utils.content_manager import ContentManager

router = APIRouter()
logger = app_logging.setup_logger("app_logger")

def return_zip(request: Request) -> StreamingResponse:
    """Return a ZIP file containing random binary data."""
    cm = ContentManager(default="index", extension="zip")
    cm.resolve(request)
    
    buffer = generate_archive(cm.file_name, "zip")
    return create_response(buffer, cm.file_name, "application/zip")

ROUTES = ["/zip", "/zips"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_zip, methods=["GET", "POST"], tags=["Archives"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_zip, methods=["GET", "POST"], tags=["Archives"])


####

def return_tar(request: Request) -> StreamingResponse:
    """Return a ZIP file containing random binary data."""
    cm = ContentManager(default="index", extension="tar")
    cm.resolve(request)
    
    buffer = generate_archive(cm.file_name, "tar")
    return create_response(buffer, cm.file_name, "application/x-tar")

ROUTES = ["/tar", "/tars"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_tar, methods=["GET", "POST"], tags=["Archives"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_tar, methods=["GET", "POST"], tags=["Archives"])
