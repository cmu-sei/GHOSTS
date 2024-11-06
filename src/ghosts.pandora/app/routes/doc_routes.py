from fastapi import APIRouter, Response, HTTPException
from io import BytesIO
from docx import Document
from faker import Faker
from utils.helper import generate_random_name
import app_logging
from config.config import allowed_extensions


logger = app_logging.setup_logger("app_logger")

router = APIRouter()
fake = Faker()


@router.get("/doc", tags=["Documents"])
@router.post("/doc", tags=["Documents"])
@router.get("/doc/{file_name:path}", tags=["Documents"])
@router.post("/doc/{file_name:path}", tags=["Documents"])
def return_doc_file(file_name: str = None) -> Response:
    """Return a Word document with random content."""
    # Log the incoming request
    logger.info(f"Received request to return a Word document. file_name: {file_name}")

    # Default to .docx if no file_name is provided
    if file_name is None:
        file_name = generate_random_name(".docx")  # Use random name generation
        logger.info(f"No file name provided. Generated file name: {file_name}")
    else:
        if not any(file_name.endswith(ext) for ext in allowed_extensions):
            logger.error(f"Invalid file extension for file name: {file_name}")
            raise HTTPException(
                status_code=400,
                detail="Invalid file extension. Allowed extensions are: "
                + ", ".join(allowed_extensions),
            )

    logger.info(f"Generating Word document with file name: {file_name}")

    document = Document()
    document.add_heading(fake.sentence(), 0)

    # Add content to the document
    p = document.add_paragraph(fake.paragraph(nb_sentences=3))
    p.add_run(fake.paragraph()).bold = True

    buf = BytesIO()
    document.save(buf)
    buf.seek(0)  # Seek back to the start of the BytesIO buffer

    # Determine the media type based on the file extension
    if file_name.endswith(".docx"):
        media_type = (
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        )
    elif file_name.endswith(".doc"):
        media_type = "application/msword"
    elif file_name.endswith(".dotx"):
        media_type = (
            "application/vnd.openxmlformats-officedocument.wordprocessingml.template"
        )
    elif file_name.endswith(".dot"):
        media_type = "application/msword"
    elif file_name.endswith(".docm"):
        media_type = "application/vnd.ms-word.document.macroEnabled.12"
    elif file_name.endswith(".dotm"):
        media_type = "application/vnd.ms-word.template.macroEnabled.12"
    elif file_name.endswith(".odt"):
        media_type = "application/vnd.oasis.opendocument.text"
    else:
        media_type = "application/octet-stream"  # Default fallback

    logger.info(
        f"Returning response with media type: {media_type} for file: {file_name}"
    )

    response = Response(content=buf.getvalue(), media_type=media_type)
    response.headers["Content-Disposition"] = f"inline; filename={file_name}"
    return response
