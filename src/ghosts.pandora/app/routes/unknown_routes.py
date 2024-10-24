from fastapi import APIRouter, HTTPException, Request
from utils.helper import generate_random_name
import random
import inspect
from config.config import endpoints
from typing import Any
import app_logging
from routes import (
    zip_routes,
    binary_routes,
    json_routes,
    csv_routes,
    text_routes,
    stylesheet_routes,
    script_routes,
    image_routes,
    onenote_routes,
    doc_routes,
    ppt_routes,
    pdf_routes,
    xlsx_routes,
    mp4_routes,
)


logger = app_logging.setup_logger("app_logger")

router = APIRouter()

# Create a mapping of endpoint names to their corresponding functions
endpoint_mapping = {
    "return_zip": zip_routes.return_zip,
    "return_binary": binary_routes.return_binary,
    "return_json": json_routes.return_json,
    "return_csv": csv_routes.return_csv,
    "return_text": text_routes.return_text,
    "return_stylesheet": stylesheet_routes.return_stylesheet,
    "return_script": script_routes.return_script,
    "return_image": image_routes.return_image,
    "return_onenote": onenote_routes.return_onenote,
    "return_doc_file": doc_routes.return_doc_file,
    "return_pdf": pdf_routes.return_pdf,
    "return_ppt": ppt_routes.return_ppt,
    "return_xlsx": xlsx_routes.return_xlsx,
    "return_mp4": mp4_routes.return_mp4,
}


@router.get("/{path_name:path}", tags=["Default Route"])
@router.post("/{path_name:path}", tags=["Default Route"])
@router.delete("/{path_name:path}", tags=["Default Route"])
@router.put("/{path_name:path}", tags=["Default Route"])
async def unknown_path(
    path_name: str, request: Request
) -> dict[str, str] | Any | object:
    """Handle unknown paths by returning a random method's result or appropriate responses for PUT/DELETE."""

    # Log the request method and path
    logger.info(f"Received {request.method} request for path: {path_name}")

    # Handle PUT and DELETE requests
    if request.method == "PUT":
        file_name_or_type = generate_random_name(".txt")  # Use random name generation
        logger.info(f"Updating resource: {file_name_or_type}")
        return {
            "message": f"Resource '{file_name_or_type}' has been updated successfully."
        }

    elif request.method == "DELETE":
        file_name_or_type = generate_random_name(".txt")
        logger.info(f"Deleting resource: {file_name_or_type}")
        return {
            "message": f"Resource '{file_name_or_type}' has been deleted successfully."
        }

    # Handle GET and POST requests
    valid_endpoints = [
        ep
        for ep in endpoints
        if ep not in {"unknown_path", "return_payloads", "return_video_feed"}
    ]
    endpoint_name = random.choice(valid_endpoints)
    logger.info(f"Selected Endpoint: {endpoint_name}")

    endpoint_function = endpoint_mapping.get(endpoint_name)

    if not callable(endpoint_function):
        logger.error("Endpoint function is not callable.")
        raise HTTPException(
            status_code=500, detail="Endpoint function is not callable."
        )

    # Prepare parameters based on the selected endpoint
    params = {}

    # Get the signature of the endpoint function
    sig = inspect.signature(endpoint_function)
    param_names = sig.parameters.keys()
    logger.info(f"Parameters avaliable {param_names}")

    # Call the selected endpoint function with the prepared parameters
    try:
        if inspect.iscoroutinefunction(endpoint_function):
            logger.info(
                f"Calling endpoint function: {endpoint_name} with params: {params}"
            )
            return await endpoint_function(**params)  # Await if the function is async
        else:
            logger.info(
                f"Calling endpoint function: {endpoint_name} with params: {params}"
            )
            return endpoint_function(**params)  # Call directly if the function is sync
    except Exception as e:
        logger.error(
            f"Error occurred while calling endpoint function: {endpoint_name} - {str(e)}"
        )
        raise HTTPException(status_code=500, detail=str(e))
