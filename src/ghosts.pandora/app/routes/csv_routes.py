import random
from faker import Faker
from fastapi import APIRouter, Request, Response

from app_logging import setup_logger
from config.config import CSV_MODEL, OLLAMA_ENABLED
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()
cm = ContentManager(default="index", extension="csv")

def return_csv(request: Request) -> Response:
    cm.resolve(request)
    
    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type="text/csv")

    num_rows = random.randint(1, 100)
    body = ""

    try:
        if OLLAMA_ENABLED:
            prompt = f"Generate a CSV with {num_rows} rows. Columns: Name, Address, Password. Realistic fake data."
            body = generate_document_with_ollama(prompt, CSV_MODEL)
            if body:
                logger.info("CSV generated via Ollama.")
        
        if not body:
            logger.info("Falling back to Faker for CSV generation.")
            body = fake.csv(
                header=("Name", "Address", "Password"),
                data_columns=("{{name}}", "{{address}}", "{{password}}"),
                num_rows=num_rows,
                include_row_ids=True,
            )
    except Exception as e:
        logger.exception("Error generating CSV.")
        return Response(content="Error generating CSV.", status_code=500)

    if cm.is_storing():
        cm.save(body.encode("utf-8"))

    filename = cm.rel_path or generate_random_name("csv")
    return Response(
        content=body.encode("utf-8"),
        media_type="text/csv",
        headers={"Content-Disposition": f'inline; filename="{filename}"'}
    )


ROUTES = ["/csv", "/csvs"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_csv, methods=["GET", "POST"], tags=["Data Structures"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_csv, methods=["GET", "POST"], tags=["Data Structures"])

