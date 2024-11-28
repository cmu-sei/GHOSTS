import logging
import os
from logging.handlers import TimedRotatingFileHandler

from config.config import (FILE_LOGGING, LOG_DIR, LOG_FILE,
                           LOG_JSON_FORMATTING, LOG_LEVEL)
from pythonjsonlogger import jsonlogger

# Create log directory dynamically if needed
os.makedirs(LOG_DIR, exist_ok=True)


def setup_logger(
    logger_name: str,
    log_file: str = None,
    level: int = None,
    enable_file_logging: bool = FILE_LOGGING,
) -> logging.Logger:
    """
    Set up a logger that logs messages to both console and optionally to a file.

    Args:
        logger_name (str): Name of the logger.
        log_file (str, optional): Path to the log file. Defaults to LOG_FILE or "app.log".
        level (int, optional): Logging level. Defaults to LOG_LEVEL or logging.INFO.
        enable_file_logging (bool, optional): Enable or disable file logging. Defaults to True.

    Returns:
        logging.Logger: Configured logger instance.
    """
    log_file = log_file or LOG_FILE or "app.log"
    level = level if level is not None else LOG_LEVEL or logging.INFO

    # Get the logger instance
    logger = logging.getLogger(logger_name)

    # Prevent duplicate handlers
    if logger.hasHandlers():
        logger.handlers.clear()

    # Set logging level
    logger.setLevel(level)

    # JSON formatter for both console and file
    json_formatter = jsonlogger.JsonFormatter(LOG_JSON_FORMATTING)

    # Console handler
    console_handler = logging.StreamHandler()
    console_handler.setFormatter(json_formatter)
    logger.addHandler(console_handler)

    # File handler (if enabled)
    if enable_file_logging:
        try:
            file_handler = TimedRotatingFileHandler(
                os.path.join(LOG_DIR, log_file),
                when="midnight",
                interval=1,
                backupCount=30,
            )
            file_handler.suffix = "%Y-%m-%d"
            file_handler.setFormatter(json_formatter)
            logger.addHandler(file_handler)
        except Exception as e:
            logger.error(f"Failed to add file handler for logger '{logger_name}': {e}")

    return logger


def configure_uvicorn_logging():
    """
    Configures Uvicorn logging to use JSON formatting and enable logging to a file.
    """
    # Disable colour output in Uvicorn by setting this environment variable
    os.environ["UVICORN_NO_COLOR"] = "1"

    json_formatter = jsonlogger.JsonFormatter(LOG_JSON_FORMATTING)
    console_handler = logging.StreamHandler()
    console_handler.setFormatter(json_formatter)

    # File handler (if enabled)
    file_handler = None
    if FILE_LOGGING:
        try:
            file_handler = TimedRotatingFileHandler(
                os.path.join(LOG_DIR, LOG_FILE),
                when="midnight",
                interval=1,
                backupCount=30,
            )
            file_handler.suffix = "%Y-%m-%d"
            file_handler.setFormatter(json_formatter)
        except Exception as e:
            logging.error(f"Failed to configure Uvicorn file handler: {e}")

    # Apply the handlers to Uvicorn loggers
    for uvicorn_logger_name in ["uvicorn", "uvicorn.error", "uvicorn.access"]:
        uv_logger = logging.getLogger(uvicorn_logger_name)
        uv_logger.handlers.clear()  # Prevent duplicate handlers
        uv_logger.addHandler(console_handler)
        if file_handler:
            uv_logger.addHandler(file_handler)
        uv_logger.propagate = False  # Avoid duplicate logs in root logger
        uv_logger.setLevel(LOG_LEVEL or logging.INFO)

    # Ensure that no colour formatting is applied by disabling colour in Uvicorn logs
    uv_logger = logging.getLogger("uvicorn")
    for handler in uv_logger.handlers:
        if isinstance(handler, logging.StreamHandler):
            handler.setFormatter(json_formatter)  # Apply JSON formatter
