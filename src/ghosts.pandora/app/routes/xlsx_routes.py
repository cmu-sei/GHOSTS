from fastapi import APIRouter, Response
from io import BytesIO
from openpyxl import Workbook
from faker import Faker
import random
import os
import app_logging
from utils.helper import generate_random_name

router = APIRouter()
fake = Faker()


logger = app_logging.setup_logger("app_logger")


@router.get("/sheets", tags=["Spreadsheets"])
@router.post("/xlsx", tags=["Spreadsheets"])
@router.get("/sheets/{file_name}", tags=["Spreadsheets"])
@router.post("/xlsx/{file_name}", tags=["Spreadsheets"])
def return_xlsx(file_name: str = None):
    """Return an Excel file with random content and a random sheet title."""

    if file_name is None:
        file_name = generate_random_name(".xlsx")  # Use random name generation
        logger.info("Generated random file name: %s", file_name)
    else:
        # Check for extension and ensure to return .xlsx in case of an exception
        base_name, ext = os.path.splitext(file_name)
        if ext.lower() not in [".xlsx", ".xls", ".xlsm", ".xltx", ".xltm"]:
            logger.warning(
                "Invalid file extension provided: %s. Changing to .xlsx", ext
            )
            file_name = (
                f"{base_name}.xlsx"  # Set to .xlsx if invalid extension is given
            )

    # Create a new Excel workbook
    workbook = Workbook()

    # Generate a random sheet title and truncate to 31 characters
    sheet_title = fake.sentence(nb_words=random.randint(2, 5))[
        :31
    ]  # Ensure title is at most 31 characters
    sheet = workbook.active
    sheet.title = sheet_title
    logger.info("Created sheet with title: %s", sheet_title)

    # Generate a random number of rows between 5 and 20
    num_rows = random.randint(5, 20)
    logger.info("Generating %d rows of random data.", num_rows)

    # Add random data to the sheet
    for i in range(num_rows):  # Add a random number of rows
        row_data = [
            fake.word()
            if random.choice([True, False])
            else fake.word()  # Randomly choose between a word or a one-word sentence
            for _ in range(3)  # Add 3 random entries per row
        ]
        sheet.append(row_data)  # Append the row data
        logger.debug("Added row %d: %s", i + 1, row_data)  # Log the added row

    # Save the workbook to a BytesIO buffer
    buf = BytesIO()
    workbook.save(buf)
    buf.seek(0)  # Reset the buffer's position to the beginning

    # Determine media type and response filename based on the provided extension
    media_type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"  # Default to .xlsx

    response = Response(content=buf.getvalue(), media_type=media_type)
    response.headers["Content-Disposition"] = f"attachment; filename={file_name}"

    logger.info("Excel file generated successfully: %s", file_name)
    return response
