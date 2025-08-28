import os
from fastapi import APIRouter, Request, Response
from faker import Faker
from config.config import OLLAMA_ENABLED, SCRIPT_MODEL
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager
from app_logging import setup_logger

router = APIRouter()
logger = setup_logger(__name__)
fake = Faker()
cm = ContentManager(default="script", extension="js")


def return_script(request: Request) -> Response:
    cm.resolve(request)

    script_type = "python" if cm.file_name.endswith(".py") else "javascript"
    media_type = "text/x-python" if script_type == "python" else "text/javascript"

    if cm.is_storing():
        if cached := cm.load():
            logger.info(f"Serving cached script: {cm.file_name}")
            return Response(
                content=cached,
                media_type=media_type,
                headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
            )

    # Generate via Ollama if available
    if OLLAMA_ENABLED:
        prompt = f"Give me a {script_type} script without any quotes around it. Just the code."
        try:
            logger.info(f"Ollama script prompt: {prompt}")
            generated = generate_document_with_ollama(prompt, SCRIPT_MODEL)
            if generated:
                logger.info(f"Ollama script generated for {cm.file_name}")
                if cm.is_storing():
                    cm.save(generated.encode("utf-8"))
                return Response(
                    content=generated.encode("utf-8"),
                    media_type=media_type,
                    headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
                )
        except Exception as e:
            logger.error(f"Ollama failed: {e}", exc_info=True)

    # Fallback to Faker
    if script_type == "javascript":
        content = f"console.log('{fake.word()}, {fake.date()}');"
    else:
        content = (
            "# Simple Python script\n"
            "import datetime\n\n"
            "def main():\n"
            f"    print('{fake.word()}, {fake.date()}')\n\n"
            "if __name__ == '__main__':\n"
            "    main()\n"
        )

    if cm.is_storing():
        cm.save(content.encode("utf-8"))

    logger.info(f"Fallback script generated: {cm.file_name}")
    return Response(
        content=content.encode("utf-8"),
        media_type=media_type,
        headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
    )


# Register routes
ROUTES = ["/script", "/scripts", "/js"]
for route in ROUTES:
    router.add_api_route(route, return_script, methods=["GET", "POST"], tags=["Web"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_script, methods=["GET", "POST"], tags=["Web"])
