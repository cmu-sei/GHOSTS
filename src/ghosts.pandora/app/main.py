import inspect
import os
import mimetypes
import time

import uvicorn
from app_logging import configure_uvicorn_logging, setup_logger
from config.config import LOG_LEVEL, OPENAPI_METADATA, PANDORA_VERSION, STORE_RESULTS, OLLAMA_ENABLED
from fastapi import FastAPI, HTTPException, Response, Request
from routes import (archive_routes, binary_routes, csv_routes, doc_routes,
                    executable_routes, html_routes, image_routes, iso_routes,
                    json_routes, onenote_routes, payload_routes, pdf_routes,
                    ppt_routes, script_routes, stylesheet_routes, text_routes,
                    unknown_routes, video_routes, voice_routes, xlsx_routes)


# Unset proxy environment variables
os.environ.pop("http_proxy", None)
os.environ.pop("https_proxy", None)
os.environ.pop("HTTP_PROXY", None)
os.environ.pop("HTTPS_PROXY", None)

logger = setup_logger(__name__)
configure_uvicorn_logging()

# Initialize FastAPI with OpenAPI metadata from config
app = FastAPI(**OPENAPI_METADATA)

def guess_media_type(path: str) -> str:
    media_type, _ = mimetypes.guess_type(path)
    return media_type or "application/octet-stream"

def is_text_type(path: str) -> bool:
    mimetype, _ = mimetypes.guess_type(path)
    return mimetype is not None and mimetype.startswith("text")

# Include routers
app.include_router(archive_routes.router)
app.include_router(voice_routes.router)
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
app.include_router(video_routes.router)
app.include_router(pdf_routes.router)
app.include_router(payload_routes.router)
app.include_router(html_routes.router)
app.include_router(executable_routes.router)
app.include_router(iso_routes.router)

@app.get("/about", tags=["Information"])
async def about() -> dict:
    """
    Provides information about the GHOSTS PANDORA server.

    Returns:
        dict: A dictionary containing version information and a message.
    """
    start = time.monotonic()

    logger.info("Received request for about information.")
    return {
        "version": PANDORA_VERSION,
        "ollama": OLLAMA_ENABLED,
        "message": "GHOSTS PANDORA server",
        "copyright": "Carnegie Mellon University. All Rights Reserved.",
        "duration": time.monotonic() - start
    }


@app.get("/{path:path}", tags=["Files"])
@app.post("/{path:path}", tags=["Files"])
def file_type_handler(request: Request, path: str) -> Response:
    start = time.monotonic()

    """Handle requests based on the file type in the path."""
    logger.info(f"Received request for file type handler with path: {path}")
    file_type = path.split(".")[-1] if "." in path else None

    # # Cache read
    # if STORE_RESULTS:
    #     cached = content_manager.get_saved_content(request)
    #     if cached is not None:
    #         media_type = guess_media_type(path)
    #         # If cached is already bytes or str, wrap it
    #         if isinstance(cached, (str, bytes)):
    #             filename = os.path.basename(request.url.path)
    #             return Response(
    #                 content=cached,
    #                 media_type=media_type,
    #                 headers={"Content-Disposition": f"inline; filename={filename}", "X-Duration": f"{time.monotonic() - start:.3f}s"}
    #             )
    #         # If cached is a full Response object (optional support)
    #         if isinstance(cached, Response):
    #             cached.headers["X-Duration"] = f"{time.monotonic() - start:.3f}s"
    #             return cached

    # Map file types to their corresponding endpoint functions
    handler_mapping = {
        # Images
        "pdf": pdf_routes.return_pdf,
        "png": image_routes.return_image,
        "gif": image_routes.return_image,
        "jpg": image_routes.return_image,
        "jpeg": image_routes.return_image,
        # "ico": image_routes.return_image,
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
        "mp4": video_routes.return_video,
        # Sound files
        "mp3": voice_routes.generate_synthesised_conversation,
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
        "tar": archive_routes.return_tar,
        "zip": archive_routes.return_zip,
        # Executables
        "exe": executable_routes.return_exe,
        "msi": executable_routes.return_msi,
        # Misc
        "iso": iso_routes.return_iso,
        "bin": binary_routes.return_binary,
        "chm": html_routes.return_chm,
        "txt": text_routes.return_text,
        "css": stylesheet_routes.return_stylesheet,
        "js": script_routes.return_script,
        "json": json_routes.return_json,
        "csv": csv_routes.return_csv,
        "payload": payload_routes.return_payloads,
        "html": html_routes.return_html,
        "htm": html_routes.return_html,
        None: html_routes.return_html,
    }

    if file_type in handler_mapping:
        logger.info(f"File type '{file_type}' is supported.")
        handler_function = handler_mapping[file_type]

        params = {}
        if "path" in inspect.signature(handler_function).parameters:
            params["path"] = path

        try:
            result = handler_function(request)

            # if STORE_RESULTS and hasattr(result, "body"):
            #     body = result.body
            #     content_manager.save_content_if_not_exists(request, body)

            result.headers["X-Duration"] = f"{time.monotonic() - start:.3f}s"
            return result

        except Exception as e:
            logger.error(f"Error in file_type_handler for '{path}': {e}")
            raise HTTPException(status_code=500, detail=str(e))
    else:
        logger.error(f"File type '{file_type}' not supported for path '{path}'")
        raise HTTPException(status_code=404, detail="File type not supported.")


app.include_router(unknown_routes.router)

if __name__ == "__main__":
    uvicorn.run(
        app,
        host="0.0.0.0",
        port=80,
        
    )
