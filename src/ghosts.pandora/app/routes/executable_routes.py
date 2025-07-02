from io import BytesIO
from fastapi import APIRouter, Request
from fastapi.responses import StreamingResponse
from faker import Faker
from utils.helper import generate_random_name
from utils.content_manager import ContentManager
from app_logging import setup_logger

router = APIRouter()
fake = Faker()
logger = setup_logger(__name__)


def return_binary(request: Request) -> StreamingResponse:
    cm = ContentManager(default="index", extension=ext)
    cm.resolve(request)

    if cm.is_storing():
        if content := cm.load():
            return StreamingResponse(
                BytesIO(content),
                media_type=cm.guess_media_type(),
                headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
            )

    payload = f"Fake {ext.upper()} content: {fake.paragraph()}".encode("utf-8")

    if cm.is_storing():
        cm.save(payload)

    buf = BytesIO(payload)
    buf.seek(0)

    logger.info(f"Returning {ext.upper()} file: {cm.file_name}")

    return StreamingResponse(
        buf,
        media_type=cm.guess_media_type(),
        headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
    )


def return_msi(request: Request) -> StreamingResponse:
    return return_binary(request, "msi", "application/x-msdownload")


def return_exe(request: Request) -> StreamingResponse:
    return return_binary(request, "exe", "application/octet-stream")


for ext, handler in [("msi", return_msi), ("exe", return_exe)]:
    for method in ["GET", "POST"]:
        router.add_api_route(f"/{ext}", handler, methods=[method], tags=["Files"])
        router.add_api_route(f"/{ext}/{{file_name}}", handler, methods=[method], tags=["Files"])
