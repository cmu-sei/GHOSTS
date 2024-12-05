from io import BytesIO

from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, TEXT_MODEL, allowed_extensions
from docx import Document
from faker import Faker
from fastapi import APIRouter, HTTPException, Response
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama

logger = setup_logger(__name__)

router = APIRouter()
fake = Faker()


@router.get("/doc", tags=["Documents"])
@router.post("/doc", tags=["Documents"])
@router.get("/doc/{file_name:path}", tags=["Documents"])
@router.post("/doc/{file_name:path}", tags=["Documents"])
def return_doc_file(file_name: str = None) -> Response:
    """Return a Word document with random content, with AI fallback if enabled."""

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

    # Try generating the content using AI (Ollama) if enabled
    if OLLAMA_ENABLED:
        try:
            prompt = f"Create a document titled '{file_name}' with several paragraphs on the topic given in the title. Only return the contents of the document and not any other content"
            logger.info(f"Sending request to Ollama model with prompt: {prompt}")

            generated_content = generate_document_with_ollama(prompt, TEXT_MODEL)

            if generated_content:
                # Add AI-generated content to the document
                p = document.add_paragraph(generated_content)
                logger.info("AI-generated content added to the document.")
            else:
                logger.warning("Ollama did not return content. Falling back to Faker.")
                # Fallback to Faker if AI fails to return content
                p = document.add_paragraph(fake.paragraph(nb_sentences=3))
                p.add_run(fake.paragraph()).bold = True
                logger.info("Faker-generated content added to the document.")
        except Exception as e:
            logger.error(f"Error generating content with Ollama: {str(e)}")
            logger.warning("Falling back to Faker for content generation.")
            # Fallback to Faker in case of AI failure
            p = document.add_paragraph(fake.paragraph(nb_sentences=3))
            p.add_run(fake.paragraph()).bold = True
            logger.info("Faker-generated content added to the document.")
    else:
        # If AI is not enabled, fallback to Faker
        logger.info(
            "Ollama is not enabled. Falling back to Faker for content generation."
        )
        p = document.add_paragraph(fake.paragraph(nb_sentences=3))
        p.add_run(fake.paragraph()).bold = True
        logger.info("Faker-generated content added to the document.")

    # Save document to a BytesIO buffer
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
