from fastapi import APIRouter
from fastapi.responses import StreamingResponse
import app_logging
from utils.helper import generate_frames

router = APIRouter()


logger = app_logging.setup_logger("app_logger")


@router.get("/video_feed", response_class=StreamingResponse, tags=["Video"])
def return_video_feed() -> StreamingResponse:
    """Return a streaming response of random video frames."""
    logger.info("Video feed request received.")

    try:
        response = StreamingResponse(
            generate_frames(), media_type="multipart/x-mixed-replace; boundary=frame"
        )
        logger.info("Video feed streaming started.")
        return response
    except Exception as e:
        logger.error("Error while generating video feed: %s", str(e))
        return {"error": "An error occurred while generating the video feed."}
