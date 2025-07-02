import random
from io import BytesIO

from app_logging import setup_logger
from faker import Faker
from fastapi import APIRouter, Request, Response
from utils.content_manager import ContentManager

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()
cm = ContentManager(default="index", extension="bin")
    
def return_binary(request: Request) -> Response:
    """
    Generate and return a binary file as a response.

    If `file_name` is provided, it will be used as the name of the file;
    otherwise, a random file name will be generated. The binary content
    is generated using Faker and ranges in size from 1000 to 300000 bytes.

    Args:
        file_name (str, optional): The name of the binary file. Defaults to None.

    Returns:
        Response: A FastAPI Response object containing binary data and
        a content disposition header to trigger a file download.
    """
    cm.resolve(request)

    # Cache check
    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type="application/octet-stream")

    # Generate random binary content
    binary_length = random.randint(1000, 3000000)
    logger.debug(f"Generating binary content of length: {binary_length} bytes")
    buf = BytesIO(fake.binary(length=binary_length))

    # Create a response with binary content and set the correct media type
    response = Response(content=buf.getvalue(), media_type="application/octet-stream")

    # Set the content disposition header for file download
    response.headers["Content-Disposition"] = f"attachment; filename={cm.file_name}"
    logger.info(f"Serving binary file: {cm.file_name}")

    if cm.is_storing():
        cm.save(buf.getvalue())

    return response


ROUTES = ["/bin", "/binary", "/binaries"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_binary, methods=["GET", "POST"], tags=["Binary"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_binary, methods=["GET", "POST"], tags=["Binary"])
