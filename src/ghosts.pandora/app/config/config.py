import app_logging

# Initialize logger
logger = app_logging.setup_logger("app_logger")

OLLAMA_ENABLED = True

# List of available endpoints
endpoints = [
    "return_zip",
    "return_binary",
    "return_json",
    "return_csv",
    "return_text",
    "return_stylesheet",
    "return_script",
    "return_image",
    "return_onenote",
    "return_doc_file",
    "return_ppt",
    "return_xlsx",
    "return_video_feed",
    "return_mp4",
    "return_payloads",
    "return_pdf",
    "unknown_path",
]

allowed_extensions = [
    ".doc",
    ".docx",
    ".dot",
    ".dotx",
    ".docm",
    ".dotm",
    ".odt",
]

# OpenAPI metadata
OPENAPI_METADATA = {
    "title": "GHOSTS PANDORA API",
    "description": "This is the API for the GHOSTS PANDORA server, designed to serve a variety of file types dynamically and support video streaming.",
    "version": "0.6.0",
    "contact": {
        "name": "Carnegie Mellon University",
        "url": "https://www.cmu.edu",
        "email": "info@cmu.edu",
    },
    "license_info": {
        "name": "MIT License",
        "url": "https://opensource.org/licenses/MIT",
    },
    "openapi_tags": [
        {
            "name": "Files",
            "description": "Endpoints for serving different file types, allowing users to retrieve various formats such as documents, images, and more.",
        },
        {
            "name": "Information",
            "description": "Endpoints for retrieving server information, including details about the API version and operational status.",
        },
        {
            "name": "Archives",
            "description": "Endpoints dedicated to serving archive files such as ZIP and TAR, enabling users to download compressed data.",
        },
        {
            "name": "Binary",
            "description": "Endpoints for serving binary files, allowing users to retrieve non-text files directly from the server.",
        },
        {
            "name": "Data Structures",
            "description": "Endpoints for handling various data structures, supporting requests and responses in structured formats.",
        },
        {
            "name": "Documents",
            "description": "Endpoints for serving document files, including DOCX, PDF, and other text-based formats.",
        },
        {
            "name": "Web",
            "description": "Endpoints for serving web-related files, such as HTML, CSS, and JavaScript, used in web applications.",
        },
        {
            "name": "Image",
            "description": "Endpoints for retrieving image files, including formats such as PNG, JPEG, and GIF.",
        },
        {
            "name": "Presentations",
            "description": "Endpoints for serving presentation files, including PPT and PPTX formats, enabling users to access slideshow content.",
        },
        {
            "name": "Spreadsheets",
            "description": "Endpoints for serving spreadsheet files, including XLS and XLSX formats, facilitating data management and analysis.",
        },
        {
            "name": "Video",
            "description": "Endpoints for serving video files, allowing users to retrieve multimedia content in formats like MP4.",
        },
        {
            "name": "Payloads",
            "description": "Endpoints for handling various payloads, enabling dynamic data generation and response formatting.",
        },
        {
            "name": "Default Route",
            "description": "A catch-all endpoint for handling requests to unknown paths or unsupported file types, providing appropriate responses.",
        },
    ],
}
