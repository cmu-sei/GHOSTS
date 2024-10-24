from fastapi import APIRouter, Response
from faker import Faker
import random
import app_logging

router = APIRouter()
fake = Faker()
logger = app_logging.setup_logger("app_logger")


@router.get("/csv/{path:path}", tags=["Data Structures"])
@router.post("/csv/{path:path}", tags=["Data Structures"])
def return_csv(path: str) -> Response:
    """Generate a CSV file with fake data for any request starting with /csv."""
    logger.info(f"Received CSV request for path: /csv/{path}")

    # Generate a random number of rows
    num_rows = random.randint(1, 100)
    logger.debug(f"Generating CSV with {num_rows} rows.")

    # Generate fake CSV data
    try:
        body = fake.csv(
            header=("Name", "Address", "Password"),
            data_columns=("{{name}}", "{{address}}", "{{password}}"),
            num_rows=num_rows,
            include_row_ids=True,
        )
        logger.info("CSV data generated successfully.")
    except Exception as e:
        logger.error(f"Error generating CSV data: {e}")
        return Response(content="Error generating CSV data.", status_code=500)

    # Create a more descriptive filename based on the fake data
    random_name = fake.name().replace(
        " ", "_"
    )  # Replace spaces with underscores for filename
    filename = f"{random_name}.csv"
    logger.debug(f"Generated filename for CSV: {filename}")

    # Create the response with the correct content type and disposition
    response = Response(content=body.encode("utf8"), media_type="text/csv")
    response.headers["Content-Disposition"] = (
        f'inline; filename="{filename}"'  # Set inline disposition
    )

    # Log response details
    logger.info(f"Returning CSV file: {filename} with {num_rows} rows.")

    return response
