import os

from app_logging import setup_logger
from config.config import VIDEO_GENERATION_ENABLED
from faker import Faker
from fastapi import APIRouter, HTTPException
from fastapi.responses import Response, StreamingResponse
from utils.helper import generate_frames
from utils.text2video import generate_video_with_cogvideox

fake = Faker()

router = APIRouter()

logger = setup_logger(__name__)

VIDEO_DIRECTORY = "static"
FALLBACK_VIDEO = "fallback.mp4"

# Ensure the static directory exists
os.makedirs("static", exist_ok=True)


@router.get("/live_stream", response_class=StreamingResponse, tags=["Video"])
@router.post("/live_stream", response_class=StreamingResponse, tags=["Video"])
def return_video_feed() -> StreamingResponse:
    """Return a live streaming response of random video frames."""
    logger.info("Video feed request received.")

    try:
        response = StreamingResponse(
            generate_frames(), media_type="multipart/x-mixed-replace; boundary=frame"
        )
        logger.info("Video feed streaming started.")
        return response
    except Exception as e:
        logger.error(f"Error while generating video feed: {str(e)}")
        return {"error": "An error occurred while generating the video feed."}


@router.get("/video", tags=["Video"])
@router.post("/video", tags=["Video"])
@router.get("/video/{file_name}", tags=["Video"])
@router.post("/video/{file_name}", tags=["Video"])
def return_video(
    file_name: str = None,
) -> Response:
    """
    Return an MP4 video file. Generate one based on a text prompt if provided.
    Falls back to a static file if generation fails.

    Args:
        file_name (str): Optional. Name of the video file to serve or generate.
        prompt (str): Optional. Text prompt to generate a video.

    Returns:
        Response: Video file as an HTTP response.
    """
    if not file_name:
        file_name = f"{fake.word()}_movie.mp4"
        logger.info("Generated random file name: %s", file_name)
    else:
        logger.info("Requested file name: %s", file_name)

    # Define path for static and generated videos
    video_path = f"{VIDEO_DIRECTORY}/{file_name}"

    if VIDEO_GENERATION_ENABLED:
        if file_name:
            # Attempt to generate the video based on the text prompt
            logger.info("Generating video from text prompt: %s", file_name)
            try:
                output_file = generate_video_with_cogvideox(
                    prompt=file_name,
                    output_filename=video_path,
                    num_frames=49,
                    guidance_scale=6,
                    seed=42,
                )
                if output_file:
                    logger.info("Video generated successfully: %s", output_file)
                else:
                    logger.warning(
                        "Failed to generate video. Falling back to static content."
                    )
            except Exception as e:
                logger.error("Error generating video: %s", e, exc_info=True)
                logger.warning("Falling back to static content.")
    else:
        logger.info("Video generation is disabled. Skipping AI generation.")

    # If the video file doesn't exist, serve fallback video
    if not os.path.isfile(video_path):
        logger.warning("Video file not found: %s. Using fallback.", video_path)
        fallback_path = (
            f"{VIDEO_DIRECTORY}/{FALLBACK_VIDEO}"  # Ensure a fallback video exists
        )
        if not os.path.isfile(fallback_path):
            logger.error(
                f"Fallback video not found: {VIDEO_DIRECTORY}/{FALLBACK_VIDEO}"
            )
            raise HTTPException(
                status_code=500,
                detail="Fallback video not available and generation failed.",
            )
        video_path = fallback_path

    # Serve the video file
    with open(video_path, "rb") as f:
        content = f.read()
        logger.debug("Read video file: %s of size: %d bytes", video_path, len(content))

    response = Response(content=content, media_type="video/mp4")
    response.headers["Content-Disposition"] = (
        f"attachment; filename={os.path.basename(video_path)}"
    )

    logger.info("Serving video file: %s", video_path)
    return response
