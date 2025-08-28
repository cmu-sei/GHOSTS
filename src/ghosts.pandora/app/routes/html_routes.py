import os
import random
import tempfile
import zipfile
from io import BytesIO

from app_logging import setup_logger
from config.config import HTML_MODEL, OLLAMA_ENABLED
from faker import Faker
from fastapi import Request, Response, APIRouter
from fastapi.responses import HTMLResponse, StreamingResponse
from utils.helper import clean_content, generate_random_name
from utils.ollama import generate_document_with_ollama
from utils.content_manager import ContentManager

logger = setup_logger(__name__)
fake = Faker()
router = APIRouter()

def return_chm(request: Request) -> StreamingResponse:
    """Return a CHM file containing random or Ollama-generated HTML content."""
    cm = ContentManager(default="index", extension="chm")
    cm.resolve(request)

    # Cache check
    if cm.is_storing():
        if content := cm.load():
            return Response(content=content, media_type="application/x-chm")

    # Use a temporary directory to store HTML content
    with tempfile.TemporaryDirectory() as temp_dir:
        index_file_path = os.path.join(temp_dir, "index.html")

        # Prepare content using Ollama if enabled
        content_list = []
        if OLLAMA_ENABLED:
            try:
                # Use Ollama to generate document content
                prompt = (
                    "Create a series of HTML pages with interesting random content."
                )
                generated_content = generate_document_with_ollama(prompt, HTML_MODEL)

                if generated_content:
                    content_list = generated_content.split(
                        "\n\n"
                    )  # Split by paragraphs
                    logger.info("Content generated successfully using Ollama.")
            except Exception as e:
                logger.error(f"Error using Ollama: {str(e)}")

        # Fallback to Faker if Ollama is not enabled or fails
        if not content_list:
            logger.info("Falling back to Faker for content generation.")
            content_list = [fake.paragraph() for _ in range(5)]

        # Write the index HTML file
        with open(index_file_path, "w", encoding="utf-8") as index_file:
            index_file.write("<html><head><title>Random CHM</title></head><body>")
            index_file.write("<h1>Random CHM Content</h1>")
            index_file.write("<ul>")

            # Create individual HTML pages
            for content in content_list:
                random_page_name = generate_random_name(".html")
                page_path = os.path.join(temp_dir, random_page_name)

                with open(page_path, "w", encoding="utf-8") as page_file:
                    page_file.write(
                        "<html><head><title>Random Page</title></head><body>"
                    )
                    page_file.write(f"<h2>{random_page_name}</h2>")
                    page_file.write(f"<p>{content}</p>")
                    page_file.write("</body></html>")

                index_file.write(
                    f'<li><a href="{random_page_name}">{random_page_name}</a></li>'
                )

            index_file.write("</ul>")
            index_file.write("</body></html>")

        # Create CHM file as a ZIP archive
        chm_buffer = BytesIO()
        with zipfile.ZipFile(chm_buffer, "w", zipfile.ZIP_DEFLATED) as chm_zip:
            for root, _, files in os.walk(temp_dir):
                for file in files:
                    file_path = os.path.join(root, file)
                    chm_zip.write(file_path, arcname=file)

        # Seek to the beginning of the BytesIO buffer
        chm_buffer.seek(0)

        # Create and return the StreamingResponse
        response = StreamingResponse(
            chm_buffer,
            media_type="application/x-chm",
            headers={"Content-Disposition": f"attachment; filename={cm.file_name}"},
        )

        if cm.is_storing():
            cm.save(chm_buffer)

    logger.info("CHM file generated successfully.")
    return response

# register routes after function is defined
ROUTES = ["/chm"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_chm, methods=["GET", "POST"], tags=["Web"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_chm, methods=["GET", "POST"], tags=["Web"])

####

def return_html(request: Request) -> HTMLResponse:
    """Return a random HTML page with inline text and images."""

    cm = ContentManager(default="index", extension="html")
    cm.resolve(request)

    # Cache check
    if cm.is_storing():
        if content := cm.load():
            return HTMLResponse(content=content)

    content = None

    # --- Try Ollama branch ---
    if OLLAMA_ENABLED:
        try:
            stripped_file_name = cm.file_name.replace("_", " ").strip(".html")
            prompt = f"Write an article for an HTML page based on the topic of {stripped_file_name}. Use short sections and natural breaks."
            ollama_result = generate_document_with_ollama(prompt, HTML_MODEL)

            if ollama_result:
                clean = clean_content(ollama_result)
                paragraphs = [
                    f"<p>{para.strip()}</p>"
                    for para in clean.split("\n\n")
                    if para.strip()
                ]

                # Sprinkle in images between paragraphs
                body_parts = []
                for para in paragraphs:
                    body_parts.append(para)
                    if random.random() < 0.4:  # 40% chance to insert an image
                        body_parts.append(
                            f"<img src='/images/{fake.uuid4()}/{fake.word()}.jpg' "
                            f"alt='{fake.word()}' style='max-width:400px;display:block;margin:1em auto;'>"
                        )

                header = f"<link rel='stylesheet' href='/css/{fake.uuid4()}/{fake.word()}.css' type='text/css'>"
                scripts = f"<script src='/scripts/{fake.uuid4()}.js'></script>"

                content = f"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                  {header}
                  <title>{fake.sentence()}</title>
                </head>
                <body>
                  <h1>{fake.catch_phrase()}</h1>
                  {"".join(body_parts)}
                </body>
                {scripts}
                </html>
                """
                logger.info("Ollama: HTML content generated successfully")
        except Exception as e:
            logger.error(f"Ollama error: {str(e)}")

    # --- Faker fallback ---
    if not content:
        title = fake.catch_phrase()
        paragraphs = [
            f"<h3>{fake.sentence().replace('.', '')}</h3>"
            f"<p>{fake.paragraph(nb_sentences=random.randint(2, 6))}</p>"
            for _ in range(random.randint(3, 8))
        ]

        body_parts = []
        for para in paragraphs:
            body_parts.append(para)
            if random.random() < 0.5:
                body_parts.append(
                    f"<img src='/images/{fake.uuid4()}/{fake.word()}.png' "
                    f"alt='{fake.word()}' style='max-width:400px;display:block;margin:1em auto;'>"
                )

        header = f"<link rel='stylesheet' href='/css/{fake.uuid4()}/{fake.word()}.css' type='text/css'>"
        scripts = f"<script src='/scripts/{fake.uuid4()}.js'></script>"

        content = f"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          {header}
          <title>{title}</title>
        </head>
        <body>
          <h1>{title}</h1>
          {"".join(body_parts)}
        </body>
        {scripts}
        </html>
        """

    # Cache if needed
    if cm.is_storing():
        cm.save(content)

    logger.info("HTML file generated successfully.")
    return HTMLResponse(content=content)

# register routes after function is defined
ROUTES = ["/", "/htm", "/html", "/web"]
for route in ROUTES:
    router.add_api_route(f"{route}", return_html, methods=["GET", "POST"], tags=["Web"])
    router.add_api_route(f"{route}/{{file_name:path}}", return_html, methods=["GET", "POST"], tags=["Web"])
