import random
from io import BytesIO
from fastapi import APIRouter, Request
from fastapi.responses import StreamingResponse
from faker import Faker

from utils.content_manager import ContentManager
from app_logging import setup_logger

router = APIRouter()
fake = Faker()
logger = setup_logger(__name__)


def return_onenote(request: Request) -> StreamingResponse:
    cm = ContentManager(default="index", extension="one")
    cm.resolve(request)

    if cm.is_storing():
        if content := cm.load():
            return StreamingResponse(
                BytesIO(content),
                media_type="application/onenote",
                headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
            )

    length = random.randint(1000, 300000)
    payload = fake.binary(length=length)

    if cm.is_storing():
        cm.save(payload)

    logger.info(f"Returning OneNote file: {cm.file_name} ({length} bytes)")

    return StreamingResponse(
        BytesIO(payload),
        media_type="application/onenote",
        headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
    )


# Register routes
ROUTES = ["/onenote"]
for route in ROUTES:
    router.add_api_route(route, return_onenote, methods=["GET", "POST"], tags=["Documents"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_onenote, methods=["GET", "POST"], tags=["Documents"])
