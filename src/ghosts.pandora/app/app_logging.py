import logging
import os
from logging.handlers import TimedRotatingFileHandler

# Define log directory and file
LOG_DIR = "logs"
LOG_FILE = "app.log"

# Create log directory if it doesn't exist
if not os.path.exists(LOG_DIR):
    os.makedirs(LOG_DIR)


# Set up logging
def setup_logger(
    logger_name: str, log_file: str = LOG_FILE, level: int = logging.INFO
) -> logging.Logger:
    """
    Set up a logger that logs messages to both console and file.

    Args:
        logger_name (str): Name of the logger.
        log_file (str, optional): Path to the log file. Defaults to "app.log".
        level (int, optional): Logging level. Defaults to logging.INFO.

    Returns:
        logging.Logger: Configured logger instance.
    """
    # Create a logger
    logger = logging.getLogger(logger_name)
    logger.setLevel(level)

    # Formatter for logs
    formatter = logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(message)s"
    )

    # Console handler
    console_handler = logging.StreamHandler()
    console_handler.setFormatter(formatter)

    # File handler with rotation
    file_handler = TimedRotatingFileHandler(
        os.path.join(LOG_DIR, log_file), when="midnight", interval=1
    )
    file_handler.setFormatter(formatter)
    file_handler.suffix = "%Y-%m-%d"  # Rotate logs daily

    # Avoid duplicate logging if the logger is already configured
    if not logger.hasHandlers():
        logger.addHandler(console_handler)
        logger.addHandler(file_handler)

    return logger


# Usage example
# app_logger = setup_logger('app_logger')
