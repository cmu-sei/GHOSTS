import random
from io import BytesIO

from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, XLSX_MODEL
from faker import Faker
from fastapi import APIRouter, Request, Response
from openpyxl import Workbook
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()
cm = ContentManager(default="index", extension="xlsx")

def return_xlsx(request: Request) -> Response:
    cm.resolve(request)

    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")

    wb = Workbook()
    sheet = wb.active
    sheet.title = fake.sentence(nb_words=random.randint(2, 5))[:31]

    for _ in range(random.randint(5, 20)):
        row_data = []
        if OLLAMA_ENABLED:
            try:
                prompt = f"Generate a row of 3 random words or short sentences for an Excel sheet."
                result = generate_document_with_ollama(prompt, XLSX_MODEL)
                row_data = result.split(",") if result else []
            except Exception as e:
                logger.warning(f"Ollama failed: {e}")
        if not row_data:
            row_data = [
                fake.word() if random.choice([True, False]) else fake.sentence(nb_words=1)
                for _ in range(3)
            ]
        sheet.append(row_data)

    buf = BytesIO()
    wb.save(buf)
    buf.seek(0)

    response = Response(content=buf.getvalue(), media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
    response.headers["Content-Disposition"] = f"attachment; filename={cm.file_name}"

    if cm.is_storing():
        cm.save(buf.getvalue())

    return response

# route registration
ROUTES = ["/xlsx", "/sheets"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_xlsx, methods=["GET", "POST"], tags=["Spreadsheets"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_xlsx, methods=["GET", "POST"], tags=["Spreadsheets"])
