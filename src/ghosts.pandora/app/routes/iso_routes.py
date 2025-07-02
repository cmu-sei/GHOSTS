from io import BytesIO

from fastapi import APIRouter, Request
from fastapi.responses import StreamingResponse
from faker import Faker

from app_logging import setup_logger
from utils.content_manager import ContentManager

router = APIRouter()
fake = Faker()
logger = setup_logger(__name__)


def return_iso(request: Request) -> StreamingResponse:
    cm = ContentManager(default="index", extension="iso")
    cm.resolve(request)

    if cm.is_storing():
        if content := cm.load(binary=True):
            return StreamingResponse(
                BytesIO(content),
                media_type="application/octet-stream",
                headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
            )

    payload = f"Fake ISO content: {fake.paragraph()}".encode("utf-8")
    if cm.is_storing():
        cm.save(payload)

    buf = BytesIO(payload)
    buf.seek(0)

    logger.info(f"Returning ISO file: {cm.file_name}")

    return StreamingResponse(
        buf,
        media_type="application/octet-stream",
        headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
    )


# register routes after function is defined
ROUTES = ["/iso"]
for route in ROUTES:
    router.add_api_route(route, return_iso, methods=["GET", "POST"], tags=["Files"])
    router.add_api_route(f"{route}/{{file_name}}", return_iso, methods=["GET", "POST"], tags=["Files"])
