import inspect
import random
from typing import Any

from fastapi import APIRouter, HTTPException, Request
from app_logging import setup_logger
from config.config import endpoints
from utils.helper import generate_random_name
from routes import (
    archive_routes,
    binary_routes,
    csv_routes,
    doc_routes,
    image_routes,
    json_routes,
    onenote_routes,
    pdf_routes,
    ppt_routes,
    script_routes,
    stylesheet_routes,
    text_routes,
    video_routes,
    xlsx_routes,
)

logger = setup_logger(__name__)
router = APIRouter()

ENDPOINT_MAP = {
    "return_zip": archive_routes.return_zip,
    "return_binary": binary_routes.return_binary,
    "return_json": json_routes.return_json,
    "return_csv": csv_routes.return_csv,
    "return_text": text_routes.return_text,
    "return_stylesheet": stylesheet_routes.return_stylesheet,
    "return_script": script_routes.return_script,
    "return_image": image_routes.return_image,
    "return_onenote": onenote_routes.return_onenote,
    "return_doc_file": doc_routes.return_doc_file,
    "return_pdf": pdf_routes.return_pdf,
    "return_ppt": ppt_routes.return_ppt,
    "return_xlsx": xlsx_routes.return_xlsx,
    "return_video": video_routes.return_video,
}


@router.api_route("/{path_name:path}", methods=["GET", "POST", "PUT", "DELETE"], tags=["Default Route"])
async def unknown_path(request: Request) -> Any:
    logger.info(f"{request.method} request for unknown path: {request.url.path}")

    if request.method == "PUT":
        name = generate_random_name(".txt")
        logger.info(f"Simulated PUT update: {name}")
        return {"message": f"Resource '{name}' has been updated successfully."}

    if request.method == "DELETE":
        name = generate_random_name(".txt")
        logger.info(f"Simulated DELETE delete: {name}")
        return {"message": f"Resource '{name}' has been deleted successfully."}

    valid = [
        ep for ep in endpoints if ep not in {"unknown_path", "return_payloads", "return_video_feed"}
    ]
    choice = random.choice(valid)
    handler = ENDPOINT_MAP.get(choice)

    if not callable(handler):
        logger.error(f"Handler for {choice} not callable.")
        raise HTTPException(status_code=500, detail="Handler not callable.")

    sig = inspect.signature(handler)
    kwargs = {}

    if "request" in sig.parameters:
        kwargs["request"] = request

    try:
        logger.info(f"Calling endpoint fallback: {choice}({list(kwargs)})")
        return await handler(**kwargs) if inspect.iscoroutinefunction(handler) else handler(**kwargs)
    except Exception as e:
        logger.error(f"Error invoking {choice}: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=str(e))
