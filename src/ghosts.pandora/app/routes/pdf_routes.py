import random
from io import BytesIO
from fastapi import APIRouter, Request, Response
from fpdf import FPDF
from faker import Faker

from config.config import OLLAMA_ENABLED, PDF_MODEL, STORE_RESULTS
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager
from app_logging import setup_logger

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()
cm = ContentManager(default="index", extension="pdf")


def sanitize_text(text: str) -> str:
    return text.encode("latin1", "replace").decode("latin1")


def return_pdf(request: Request) -> Response:
    cm.resolve(request)

    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type="application/pdf")

    title = fake.sentence()
    if OLLAMA_ENABLED:
        try:
            raw_title = generate_document_with_ollama("Provide a single creative title for a document.", PDF_MODEL)
            title = raw_title.strip() or title
        except Exception as e:
            logger.error(f"Ollama title gen error: {e}", exc_info=True)

    logger.info(f"PDF title: {title}")

    pdf = FPDF()
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()
    pdf.set_font("Arial", "B", 16)
    pdf.cell(200, 10, txt=sanitize_text(title), ln=True, align="C")
    pdf.ln(10)
    pdf.set_font("Arial", "", 12)

    response_content = None
    if OLLAMA_ENABLED:
        try:
            prompt = f"Provide a few paragraphs about the topic: '{title}'. Only return the content."
            response_content = generate_document_with_ollama(prompt, PDF_MODEL).strip()
        except Exception as e:
            logger.error(f"Ollama content gen error: {e}", exc_info=True)

    for _ in range(random.randint(5, 15)):
        paragraph = response_content or fake.sentence()
        pdf.multi_cell(0, 10, txt=sanitize_text(paragraph))

    buf = BytesIO(pdf.output(dest="S").encode("latin1"))
    buf.seek(0)

    if cm.is_storing():
        cm.save(buf.getvalue())

    logger.info(f"Returning PDF: {cm.file_name}")
    response = Response(content=buf.getvalue(), media_type="application/pdf")
    response.headers["Content-Disposition"] = f"inline; filename={cm.file_name}"
    return response


# Register routes
ROUTES = ["/pdf"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_pdf, methods=["GET", "POST"], tags=["Documents"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_pdf, methods=["GET", "POST"], tags=["Documents"])
