import os
import random
from io import BytesIO

from app_logging import setup_logger
from config.config import OLLAMA_ENABLED, XLSX_MODEL
from faker import Faker
from fastapi import APIRouter, Response
from openpyxl import Workbook
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama

router = APIRouter()
fake = Faker()

logger = setup_logger(__name__)


@router.get("/sheets", tags=["Spreadsheets"])
@router.post("/xlsx", tags=["Spreadsheets"])
@router.get("/sheets/{file_name}", tags=["Spreadsheets"])
@router.post("/xlsx/{file_name}", tags=["Spreadsheets"])
def return_xlsx(file_name: str = None):
    """Return an Excel file with random content and a random sheet title."""

    # If no file name provided, generate one
    if file_name is None:
        file_name = generate_random_name(".xlsx")
        logger.info(f"Generated random file name: {file_name}")
    else:
        base_name, ext = os.path.splitext(file_name)
        if ext.lower() not in [".xlsx", ".xls", ".xlsm", ".xltx", ".xltm"]:
            logger.warning(f"Invalid file extension provided: {ext}. Changing to .xlsx")
            file_name = (
                f"{base_name}.xlsx"  # Default to .xlsx if extension is incorrect
            )

    logger.debug(f"File name after extension check: {file_name}")

    # Create a new Excel workbook
    workbook = Workbook()
    logger.debug("Created new Excel workbook.")

    # Generate random sheet title (max 31 characters for Excel)
    sheet_title = fake.sentence(nb_words=random.randint(2, 5))[:31]
    sheet = workbook.active
    sheet.title = sheet_title
    logger.info(f"Created sheet with title: {sheet_title}")

    # Generate a random number of rows between 5 and 20
    num_rows = random.randint(5, 20)
    logger.debug(f"Generating {num_rows} rows of random data.")

    # Generate random data for each row: either using AI (Ollama) or fallback to Faker
    for i in range(num_rows):
        if OLLAMA_ENABLED:
            prompt = f"Generate a row of 3 random words or short sentences for an Excel sheet, row {i + 1}."
            logger.info(
                f"Requesting AI-based content for row {i + 1} with prompt: {prompt}"
            )

            try:
                # Get the generated content from Ollama
                ai_generated_content = generate_document_with_ollama(prompt, XLSX_MODEL)

                if ai_generated_content:
                    row_data = ai_generated_content.split(
                        ","
                    )  # Split the content into columns
                    logger.debug(f"Row {i + 1} generated using AI: {row_data}")
                else:
                    # If no content is returned from Ollama, fallback to Faker
                    logger.warning(
                        f"No content returned from Ollama for row {i + 1}, falling back to Faker."
                    )
                    row_data = [
                        fake.word()
                        if random.choice([True, False])
                        else fake.sentence(nb_words=1)
                        for _ in range(3)  # Add 3 random entries per row
                    ]
                    logger.debug(f"Row {i + 1} generated using Faker: {row_data}")
            except Exception as e:
                # Log the error and fallback to Faker
                logger.error(
                    f"Error generating content with Ollama for row {i + 1}: {str(e)}"
                )
                logger.warning(f"Falling back to Faker for row {i + 1}.")
                row_data = [
                    fake.word()
                    if random.choice([True, False])
                    else fake.sentence(nb_words=1)
                    for _ in range(3)  # Add 3 random entries per row
                ]
                logger.debug(f"Row {i + 1} generated using Faker: {row_data}")
        else:
            # Fallback to Faker if Ollama is disabled
            row_data = [
                fake.word()
                if random.choice([True, False])
                else fake.sentence(nb_words=1)
                for _ in range(3)  # Add 3 random entries per row
            ]
            logger.debug(f"Row {i + 1} generated using Faker: {row_data}")

        sheet.append(row_data)  # Append row to the sheet

    # Save the workbook to a BytesIO buffer
    buf = BytesIO()
    workbook.save(buf)
    buf.seek(0)  # Reset the buffer to the beginning
    logger.debug("Workbook saved to buffer.")

    # Determine media type for response
    media_type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    response = Response(content=buf.getvalue(), media_type=media_type)
    response.headers["Content-Disposition"] = f"attachment; filename={file_name}"

    logger.info(f"Excel file generated successfully: {file_name}")
    return response
