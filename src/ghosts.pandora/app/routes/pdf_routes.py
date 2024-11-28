import random
from io import BytesIO

from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, PDF_MODEL
from faker import Faker
from fastapi import APIRouter, Response
from fpdf import FPDF
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama

logger = setup_logger(__name__)
fake = Faker()

router = APIRouter()


def sanitize_text(text: str) -> str:
    """Sanitize text to ensure it can be encoded in 'latin1'."""
    return text.encode("latin1", "replace").decode("latin1")


@router.get("/pdf", tags=["Documents"])
@router.post("/pdf", tags=["Documents"])
@router.get("/pdf/{path:path}", tags=["Documents"])
@router.post("/pdf/{path:path}", tags=["Documents"])
def return_pdf() -> Response:
    """Generate and return a random PDF document with a title and content based on that title."""
    logger.info("Received request to generate PDF")

    if OLLAMA_ENABLED:
        title_prompt = "Provide a single creative title for a document."
        logger.info("Requesting title from Ollama with prompt: %s", title_prompt)
        try:
            response_title = generate_document_with_ollama(title_prompt, PDF_MODEL)
            title = (
                response_title.strip()
                if response_title and response_title.strip()
                else generate_random_name()
            )
        except Exception as e:
            logger.error(f"Error while using Ollama for title: {e}", exc_info=True)
            title = generate_random_name()
    else:
        title = generate_random_name()

    logger.info(f"Generated title: {title}")

    # Step 2: Create a PDF document
    logger.info("Initializing PDF generation")
    pdf = FPDF()
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()

    # Step 3: Set default font and add title
    pdf.set_font("Arial", "B", size=16)
    sanitized_title = sanitize_text(title)
    pdf.cell(200, 10, txt=sanitized_title, ln=True, align="C")
    pdf.ln(10)

    # Step 4: Add random text content
    logger.info("Adding text content based on title to PDF")
    pdf.set_font("Arial", size=12)
    content_prompt = f"Provide a few paragraphs about the topic: '{title}, this should be a text only response'."

    # Attempt to get content from Ollama once
    if OLLAMA_ENABLED:
        try:
            response_content = generate_document_with_ollama(content_prompt, PDF_MODEL)
        except Exception as e:
            logger.error(f"Error while using Ollama for content: {e}", exc_info=True)
            response_content = None
    else:
        response_content = None  # Set to None if Ollama is not enabled

    # Fallback to Faker-generated content if Ollama fails or is disabled
    for _ in range(random.randint(5, 15)):
        if response_content and response_content.strip():
            content = response_content.strip()  # Use Ollama response if valid
        else:
            content = fake.sentence()  # Default to Faker content

        sanitized_content = sanitize_text(content)
        pdf.multi_cell(0, 10, txt=sanitized_content)

    # Step 5: Save the PDF
    pdf_output = BytesIO()
    pdf_data = pdf.output(dest="S").encode("latin1")
    pdf_output.write(pdf_data)
    pdf_output.seek(0)

    if len(pdf_output.getvalue()) == 0:
        logger.error("Generated PDF is empty")
        raise ValueError("Generated PDF has no content")

    # Step 6: Generate a random filename and return the response
    pdf_file_name = f"{fake.word()}_document.pdf"
    logger.info(f"Generated PDF filename: {pdf_file_name}")

    response = Response(content=pdf_output.getvalue(), media_type="application/pdf")
    response.headers["Content-Disposition"] = f"inline; filename={pdf_file_name}"

    logger.info("PDF generated and response prepared successfully.")
    return response
