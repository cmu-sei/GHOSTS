import os
from app_logging import setup_logger
from config.config import STORE_RESULTS, VIDEO_GENERATION_ENABLED
from faker import Faker
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import Response, StreamingResponse
from utils.helper import generate_frames
from utils.text2video import generate_video_with_cogvideox
from utils.content_manager import ContentManager

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()

VIDEO_DIR = "static"
FALLBACK = "fallback.mp4"
os.makedirs(VIDEO_DIR, exist_ok=True)


def return_video_feed(_: Request) -> StreamingResponse:
    logger.info("Live stream request received.")
    try:
        return StreamingResponse(
            generate_frames(),
            media_type="multipart/x-mixed-replace; boundary=frame",
        )
    except Exception as e:
        logger.error(f"Live stream failure: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail="Streaming error")


def return_video(request: Request) -> Response:
    file_name = file_name or f"{fake.word()}_movie.mp4"
    cm = ContentManager(directory=VIDEO_DIR, file_name=file_name)
    cm.resolve(request)

    if cm.is_storing():
        if content := cm.load():
            logger.info(f"Serving cached video: {cm.file_name}")
            return _video_response(content, cm.file_name)

    if VIDEO_GENERATION_ENABLED:
        try:
            logger.info(f"Generating video from prompt: {cm.file_name}")
            output = generate_video_with_cogvideox(
                prompt=cm.file_name,
                output_filename=cm.full_path,
                num_frames=49,
                guidance_scale=6,
                seed=42,
            )
            if output and os.path.exists(output):
                with open(output, "rb") as f:
                    content = f.read()
                if cm.is_storing():
                    cm.save(content)
                return _video_response(content, cm.file_name)
        except Exception as e:
            logger.error(f"Video generation error: {e}", exc_info=True)

    fallback_path = os.path.join(VIDEO_DIR, FALLBACK)
    if not os.path.exists(fallback_path):
        logger.error(f"Fallback video missing: {fallback_path}")
        raise HTTPException(
            status_code=500,
            detail="Fallback video unavailable and generation failed.",
        )

    with open(fallback_path, "rb") as f:
        content = f.read()
    return _video_response(content, FALLBACK)


def _video_response(content: bytes, name: str) -> Response:
    response = Response(content=content, media_type="video/mp4")
    response.headers["Content-Disposition"] = f"attachment; filename={name}"
    return response


# ROUTES
LIVE_ROUTES = ["/live_stream"]
VIDEO_ROUTES = ["/video"]

for route in LIVE_ROUTES:
    router.add_api_route(route, return_video_feed, methods=["GET", "POST"], tags=["Video"])

for route in VIDEO_ROUTES:
    router.add_api_route(route, return_video, methods=["GET", "POST"], tags=["Video"])
    router.add_api_route(f"{route}/{{file_name}}", return_video, methods=["GET", "POST"], tags=["Video"])
