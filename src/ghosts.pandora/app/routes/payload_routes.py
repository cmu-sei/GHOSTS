from fastapi import APIRouter, Response, HTTPException, Request
import configparser
import os
import app_logging

router = APIRouter()


logger = app_logging.setup_logger("app_logger")

# Load the configuration
config = configparser.ConfigParser()
config_path = os.path.join("app", "config", "app.config")
logger.info("Loading configuration from %s", config_path)
config.read(config_path)


@router.get("/payloads/{path_name:path}", tags=["Payloads"])
async def return_payloads(path_name: str, request: Request) -> Response:
    """Serve predefined payloads based on the requested path."""
    logger.info("Received request for payloads at path: %s", path_name)
    payloads = config["payloads"]

    for key in payloads:
        payload = payloads[key].strip()  # Ensure no extra whitespace
        payload_url, payload_file, payload_header = payload.split(",")

        logger.debug("Checking payload: %s", key)
        if path_name.startswith(payload_url):
            logger.info(
                "Serving %s for path %s with headers %s",
                payload_file,
                path_name,
                payload_header,
            )
            try:
                file_path = os.path.join("app", "payloads", payload_file)
                with open(file_path, "rb") as f:
                    content = f.read()
                    response = Response(content=content, media_type=payload_header)
                    response.headers["Content-Disposition"] = (
                        f"inline; filename={payload_file}"
                    )
                    logger.info("Successfully served payload: %s", payload_file)
                    return response
            except FileNotFoundError:
                logger.error("File not found: %s", payload_file)
                raise HTTPException(
                    status_code=404, detail=f"File {payload_file} not found."
                )

    logger.warning("Payload not found for path: %s", path_name)
    raise HTTPException(status_code=404, detail="Payload not found.")
