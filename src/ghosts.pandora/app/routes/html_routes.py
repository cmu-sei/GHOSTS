from fastapi import APIRouter
from fastapi.responses import StreamingResponse, HTMLResponse
from faker import Faker
import app_logging
import os
import tempfile
import zipfile
from io import BytesIO
from utils.helper import generate_random_name
import random

router = APIRouter()
fake = Faker()

logger = app_logging.setup_logger("app_logger")


@router.get("/chm", tags=["Web"])
@router.post("/chm", tags=["Web"])
@router.get("/chm/{file_name}", tags=["Web"])
@router.post("/chm/{file_name}", tags=["Web"])
def return_chm(file_name: str = None) -> StreamingResponse:
    """Return a CHM file containing random HTML content."""

    if file_name is None:
        file_name = generate_random_name(".chm")
    elif not file_name.endswith(".chm"):
        file_name += ".chm"  # Add .chm extension if not present

    logger.info(f"Generating CHM file: {file_name}")

    # Create a temporary directory to hold the HTML files
    with tempfile.TemporaryDirectory() as temp_dir:
        index_file_path = os.path.join(temp_dir, "index.html")

        # Create an index HTML file
        with open(index_file_path, "w", encoding="utf-8") as index_file:
            index_file.write("<html><head><title>Random CHM</title></head><body>")
            index_file.write("<h1>Random CHM Content</h1>")
            index_file.write("<ul>")

            # Generate random HTML content
            for _ in range(5):  # Change this number to create more or fewer pages
                random_page_name = generate_random_name(".html")
                random_content = fake.paragraph()
                page_path = os.path.join(temp_dir, random_page_name)

                # Create an HTML file for each random page
                with open(page_path, "w", encoding="utf-8") as page_file:
                    page_file.write(
                        "<html><head><title>Random Page</title></head><body>"
                    )
                    page_file.write(f"<h2>{random_page_name}</h2>")
                    page_file.write(f"<p>{random_content}</p>")
                    page_file.write("</body></html>")

                index_file.write(
                    f'<li><a href="{random_page_name}">{random_page_name}</a></li>'
                )

            index_file.write("</ul>")
            index_file.write("</body></html>")

        # Create a CHM file by first creating a ZIP archive
        chm_buffer = BytesIO()
        with zipfile.ZipFile(chm_buffer, "w", zipfile.ZIP_DEFLATED) as chm_zip:
            # Add the index file and other HTML files to the ZIP
            for root, _, files in os.walk(temp_dir):
                for file in files:
                    file_path = os.path.join(root, file)
                    chm_zip.write(file_path, arcname=file)

        # Seek to the beginning of the BytesIO buffer
        chm_buffer.seek(0)

        # Create the StreamingResponse
        response = StreamingResponse(
            chm_buffer,
            media_type="application/x-chm",
            headers={"Content-Disposition": f"attachment; filename={file_name}"},
        )

    logger.info("CHM file generated successfully.")

    return response


@router.get("/", tags=["Information"])
@router.get("/html", tags=["Web"])
@router.post("/html", tags=["Web"])
@router.get("/html/{file_name}", tags=["Web"])
@router.post("/html/{file_name}", tags=["Web"])
def return_html(file_name: str = None) -> HTMLResponse:
    """Return a random HTML file with content."""

    if file_name is None:
        file_name = generate_random_name(".html")
    elif not file_name.endswith(".html"):
        file_name += ".html"  # Add .html extension if not present

    logger.info(f"Generating HTML file: {file_name}")

    # Create random HTML content
    title = fake.text()

    body = ""
    for _ in range(random.randint(1, 20)):
        if random.randint(2, 100) > 55:
            body = body + f"<h3>{fake.sentence().replace('.','')}</h3>"
            body = (
                body + f"<p>{fake.paragraph(nb_sentences=random.randint(1, 100))}</p>"
            )
            if random.randint(1, 100) > 85:
                body = (
                    body
                    + f"<img src='images/{fake.word()}.png?h={random.randint(80, 200)}&w={random.randint(200, 400)}'/>"
                )

    header = f'<script type="text/javascript" src="/scripts/{fake.uuid4()}.js"></script><link rel="stylesheet" href="/css/{fake.uuid4()}/{fake.word()}.css" type="text/css" />'
    html_content = f"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        {header}
        <title>{title}</title>
    </head>
    <body>
        {body}
    </body>
    </html>
    """

    # Create the HTML response
    response = HTMLResponse(content=html_content)

    logger.info("HTML file generated successfully.")

    return response
