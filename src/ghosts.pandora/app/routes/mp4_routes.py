import os
import app_logging
from fastapi import APIRouter, Response
from utils.helper import generate_video_from_frames
from faker import Faker

router = APIRouter()
fake = Faker()


logger = app_logging.setup_logger("app_logger")


@router.get("/mp4", tags=["Video"])
@router.post("/mp4", tags=["Video"])
@router.get("/mp4/{file_name}", tags=["Video"])
@router.post("/mp4/{file_name}", tags=["Video"])
def return_mp4(file_name: str = None) -> Response:
    """Return an MP4 video file, generate fake one if not passed."""
    # Generate a random file name if not provided
    if not file_name:
        file_name = f"{fake.word()}_movie.mp4"
        logger.info("Generated random file name: %s", file_name)
    else:
        logger.info("Requested file name: %s", file_name)

    # Ensure static directory exists
    os.makedirs("static", exist_ok=True)

    # Specify the path for the test movie
    test_movie = f"static/{file_name}"

    # Check if the file exists; if not, create it using the generate_frames function
    if not os.path.isfile(test_movie):
        logger.debug("File does not exist, generating video: %s", test_movie)
        generate_video_from_frames(test_movie)
        logger.info("Generated video file: %s", test_movie)

    # Open the video file and prepare the response
    with open(test_movie, "rb") as f:
        content = f.read()
        logger.debug("Read video file: %s of size: %d bytes", test_movie, len(content))

    response = Response(content=content, media_type="video/mp4")
    response.headers["Content-Disposition"] = f"attachment; filename={file_name}"

    logger.info("Serving video file: %s", file_name)
    return response


#    finally:
# Delete the video file after sending the response
#        if os.path.isfile(test_movie):
#            os.remove(test_movie)
#            logger.info("Deleted temporary video file: %s", test_movie)
