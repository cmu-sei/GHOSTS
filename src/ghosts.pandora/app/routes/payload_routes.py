import configparser
import os

from app_logging import setup_logger
from fastapi import APIRouter, HTTPException, Request, Response

router = APIRouter()

logger = setup_logger(__name__)

# Load the configuration
CONFIG_SECTION = "payloads"
config = configparser.ConfigParser()
config_path = os.path.join("config", "app.config")
logger.info(f"Loading configuration from {config_path}")
if not config.read(config_path):
    logger.error(f"Configuration file not found or unreadable: {config_path}")
    raise RuntimeError(f"Configuration file not found: {config_path}")


@router.get("/payloads/{path_name:path}", tags=["Payloads"])
async def return_payloads(path_name: str, request: Request) -> Response:
    """
    Serve predefined payloads based on the requested path.
    Payload configurations are read from the app.config file.
    """
    logger.info(f"Received request for payloads at path: {path_name}")

    # Validate configuration section
    if CONFIG_SECTION not in config:
        logger.error(f"Missing '{CONFIG_SECTION}' section in configuration file.")
        raise HTTPException(status_code=500, detail="Configuration error.")

    payloads = config[CONFIG_SECTION]
    matched_payload = None

    # Search for matching payload configuration
    for key, payload in payloads.items():
        logger.debug(f"Evaluating payload: key={key}, value={payload}")
        try:
            payload_url, payload_file, payload_header = map(
                str.strip, payload.split(",")
            )
        except ValueError:
            logger.warning(f"Invalid payload format for key={key}: {payload}")
            continue

        if path_name.startswith(payload_url):
            matched_payload = (payload_file, payload_header)
            break

    # Handle unmatched paths
    if not matched_payload:
        logger.warning(f"No payload match found for path: {path_name}")
        raise HTTPException(status_code=404, detail="Payload not found.")

    payload_file, payload_header = matched_payload
    file_path = os.path.join("app", "payloads", payload_file)

    logger.info(f"Match found: Serving {payload_file} with header {payload_header}")

    # Read and serve the payload file
    try:
        with open(file_path, "rb") as f:
            content = f.read()
            logger.debug(f"Read {len(content)} bytes from file: {payload_file}")

        response = Response(content=content, media_type=payload_header)
        response.headers["Content-Disposition"] = f"inline; filename={payload_file}"
        logger.info(f"Successfully served payload: {payload_file}")
        return response

    except FileNotFoundError:
        logger.error(f"File not found: {file_path}")
        raise HTTPException(status_code=404, detail=f"File {payload_file} not found.")
    except Exception as e:
        logger.error(
            f"Unexpected error while serving payload: {file_path}: {e}", exc_info=True
        )
        raise HTTPException(status_code=500, detail="Internal server error.")
