import time

import requests
from app_logging import setup_logger
from config.config import OLLAMA_API_URL, OLLAMA_TIMEOUT

logger = setup_logger(__name__)


def generate_document_with_ollama(
    prompt: str, model: str, timeout: int = OLLAMA_TIMEOUT
):
    """
    Generate a document using the Ollama API with a specified prompt and model.

    Args:
        prompt (str): The input prompt to generate a document.
        model (str): The model name to use for the generation.

    Returns:
        str | None: The generated document text or None if an error occurs.
    """
    logger.info("Preparing to send request to Ollama model.")
    payload = {"model": model, "prompt": prompt, "stream": False}
    logger.debug(f"Payload: {payload}")

    try:
        # Record the start time
        start_time = time.time()

        # Make the request to the Ollama API
        logger.info(
            f"Sending request to Ollama API at {OLLAMA_API_URL} with timeout {timeout}s."
        )
        response = requests.post(OLLAMA_API_URL, json=payload, timeout=timeout)

        # Calculate elapsed time
        elapsed_time = time.time() - start_time
        logger.info(f"Response received in {elapsed_time:.2f} seconds.")

        # Process the response
        if response.status_code == 200:
            response_data = response.json()
            logger.debug(f"Response JSON: {response_data}")
            generated_text = response_data.get("response", "")
            logger.info(
                f"Document successfully generated. Text length: {len(generated_text)} characters."
            )
            return generated_text
        else:
            logger.error(
                f"Failed to generate document. HTTP {response.status_code}: {response.text}"
            )
            return None

    except requests.Timeout:
        logger.warning(f"Request to Ollama API timed out after {timeout}s.")
    except requests.RequestException as request_err:
        logger.error(
            f"HTTP request error while communicating with Ollama: {request_err}",
            exc_info=True,
        )
    except Exception as e:
        logger.error(f"Unexpected error occurred: {e}", exc_info=True)

    return None
