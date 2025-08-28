import random
from faker import Faker
from fastapi import APIRouter, Request, Response

from utils.content_manager import ContentManager
from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, TEXT_MODEL
from utils.ollama import generate_document_with_ollama

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()
cm = ContentManager(default="index", extension="txt")

def return_text(request: Request) -> Response:
    cm.resolve(request)
    
    # Cache check
    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type="text/plain")

    try:
        if OLLAMA_ENABLED:
            prompt = f"Create a document titled '{cm.file_name}' with several paragraphs on the topic given in the title. Only return the contents of the document and not any other content."
            logger.info("Prompt to Ollama: %s", prompt)

            content = generate_document_with_ollama(prompt, TEXT_MODEL)
            
        if not content:

            # Faker fallback
            heading = fake.sentence(nb_words=random.randint(3, 6)).strip(".")
            sections = [
                (fake.sentence(nb_words=random.randint(3, 6)).strip("."), 
                fake.paragraph(nb_sentences=random.randint(20, 150)))
                for _ in range(random.randint(1, 3))
            ]

            parts = [f"<h1>{heading}</h1>"] + [f"<h2>{h}</h2>\n<p>{p}</p>" for h, p in sections]
            content = "\n".join(parts)

        response = Response(content=content.encode("utf-8"), media_type="text/plain")
        
        if cm.is_storing():
            cm.save(content)

        return response

    except Exception as e:
        logger.exception("Error generating document.")
        return Response(
            content="An unexpected error occurred while generating the document.",
            status_code=500,
        )


# register routes after function is defined
ROUTES = ["/txt", "/text"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_text, methods=["GET", "POST"], tags=["Documents"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_text, methods=["GET", "POST"], tags=["Documents"])
