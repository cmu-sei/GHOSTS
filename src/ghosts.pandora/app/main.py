import uvicorn
from fastapi import FastAPI, Response, HTTPException

from routes import (
    zip_routes,
    binary_routes,
    json_routes,
    csv_routes,
    text_routes,
    stylesheet_routes,
    script_routes,
    image_routes,
    html_routes,
    onenote_routes,
    doc_routes,
    executable_routes,
    iso_routes,
    ppt_routes,
    xlsx_routes,
    mp4_routes,
    video_routes,
    payload_routes,
    pdf_routes,
    unknown_routes,
)
import configparser
import os
import app_logging
import inspect
from config.config import OPENAPI_METADATA

# Unset proxy environment variables
os.environ.pop("http_proxy", None)
os.environ.pop("https_proxy", None)
os.environ.pop("HTTP_PROXY", None)
os.environ.pop("HTTPS_PROXY", None)


# Initialize logger
logger = app_logging.setup_logger("app_logger")

# Load configuration
config = configparser.ConfigParser()
config.read(os.path.join("app", "config", "app.config"))

# Initialize FastAPI with OpenAPI metadata from config
app = FastAPI(
    **OPENAPI_METADATA,  # Unpacking the OpenAPI metadata
)


# Include routers
app.include_router(zip_routes.router)
app.include_router(binary_routes.router)
app.include_router(json_routes.router)
app.include_router(csv_routes.router)
app.include_router(text_routes.router)
app.include_router(stylesheet_routes.router)
app.include_router(script_routes.router)
app.include_router(image_routes.router)
app.include_router(onenote_routes.router)
app.include_router(doc_routes.router)
app.include_router(ppt_routes.router)
app.include_router(xlsx_routes.router)
app.include_router(mp4_routes.router)
app.include_router(video_routes.router)
app.include_router(pdf_routes.router)
app.include_router(payload_routes.router)
app.include_router(html_routes.router)
app.include_router(executable_routes.router)
app.include_router(iso_routes.router)


@app.get("/", tags=["Information"])
async def hello_world() -> Response:
    """
    Returns a simple 'Hello World' message.

    Returns:
        Response: A response containing 'Hello World'.
    """
    logger.info("Received request for Hello World endpoint.")
    try:
        response = Response(content="Hello World", status_code=200)
        logger.info("Successfully generated Hello World response.")
        return response
    except Exception as e:
        logger.error(f"Error in Hello World endpoint: {e}")
        raise HTTPException(status_code=500, detail="Internal Server Error")


@app.get("/about", tags=["Information"])
async def about() -> dict:
    """
    Provides information about the GHOSTS PANDORA server.

    Returns:
        dict: A dictionary containing version information and a message.
    """
    logger.info("Received request for about information.")
    return {
        "version": "0.6.0",
        "message": "GHOSTS PANDORA server",
        "copyright": "Carnegie Mellon University. All Rights Reserved.",
    }


@app.get("/{path:path}", tags=["Files"])
@app.post("/{path:path}", tags=["Files"])
def file_type_handler(path: str) -> Response:
    """Handle requests based on the file type in the path."""
    logger.info(f"Received request for file type handler with path: {path}")
    file_type = path.split(".")[-1] if "." in path else None

    # Map file types to their corresponding endpoint functions
    handler_mapping = {
        # Images
        "pdf": pdf_routes.return_pdf,
        "png": image_routes.return_image,
        "gif": image_routes.return_image,
        "jpg": image_routes.return_image,
        "jpeg": image_routes.return_image,
        "ico": image_routes.return_image,
        # Documents
        "doc": doc_routes.return_doc_file,
        "docx": doc_routes.return_doc_file,
        "dotx": doc_routes.return_doc_file,
        "dot": doc_routes.return_doc_file,
        "docm": doc_routes.return_doc_file,
        "dotm": doc_routes.return_doc_file,
        "odt": doc_routes.return_doc_file,
        "one": onenote_routes.return_onenote,
        # Video files
        "mp4": mp4_routes.return_mp4,
        # Spreadsheets
        "xls": xlsx_routes.return_xlsx,
        "xlsx": xlsx_routes.return_xlsx,
        "xlsm": xlsx_routes.return_xlsx,
        "xlsb": xlsx_routes.return_xlsx,
        "xltm": xlsx_routes.return_xlsx,
        "xla": xlsx_routes.return_xlsx,
        "xlam": xlsx_routes.return_xlsx,
        "ods": xlsx_routes.return_xlsx,
        # Presentations
        "ppt": ppt_routes.return_ppt,
        "pptx": ppt_routes.return_ppt,
        "potx": ppt_routes.return_ppt,
        "pot": ppt_routes.return_ppt,
        "ppsx": ppt_routes.return_ppt,
        "pps": ppt_routes.return_ppt,
        "pptm": ppt_routes.return_ppt,
        "potm": ppt_routes.return_ppt,
        "ppsm": ppt_routes.return_ppt,
        "odp": ppt_routes.return_ppt,
        # Compressed Files
        "tar": zip_routes.return_tar,
        "gz": zip_routes.return_gz,
        "zip": zip_routes.return_zip,
        # Executables
        "exe": executable_routes.return_exe,
        "msi": executable_routes.return_msi,
        # Misc
        "iso": iso_routes.return_iso,
        "bin": binary_routes.return_binary,
        "chm": html_routes.return_chm,
        "html": html_routes.return_html,
        "txt": text_routes.return_text,
        "css": stylesheet_routes.return_stylesheet,
        "js": script_routes.return_script,
        "json": json_routes.return_json,
        None: text_routes.return_text,
    }

    # Check if the file type has a corresponding handler
    if file_type in handler_mapping:
        logger.info(f"File type '{file_type}' is supported.")
        handler_function = handler_mapping[file_type]

        # Prepare parameters if necessary (depends on your handler's requirements)
        params = {}
        if "path" in inspect.signature(handler_function).parameters:
            params["path"] = path

        # Call the handler function
        try:
            return handler_function(**params)
        except Exception as e:
            logger.error(f"Error in file_type_handler for '{path}': {e}")
            raise HTTPException(status_code=500, detail=str(e))
    else:
        logger.error(f"File type '{file_type}' not supported for path '{path}'")
        raise HTTPException(status_code=404, detail="File type not supported.")


app.include_router(unknown_routes.router)

# Run the app with Uvicorn
if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=80)
