import os
import argparse
import ollama
from typing import List

# Default base directory for the models
DEFAULT_BASE_DIR = "content-models"
DEFAULT_MODELS = [
    "activity",
    "chat",
    "social",
    "excel_content",
    "web_content",
    "img_content",
    "lessons",
]


def create_models(base_dir: str, models: List[str], verbose: bool) -> None:
    """Creates Ollama models from specified directories and model files.

    Args:
        base_dir (str): Base directory where model folders are located.
        models (List[str]): List of model names to create.
        verbose (bool): Flag indicating whether verbose mode is enabled.
    """
    for model in models:
        model_dir = os.path.join(base_dir, model)
        modelfile_path = os.path.join(model_dir, "modelfile")

        if os.path.isdir(model_dir) and os.path.isfile(modelfile_path):
            with open(modelfile_path, "r") as file:
                modelfile_content = file.read()
            if verbose:
                print(f"Creating model '{model}' from '{modelfile_path}'...")

            try:
                ollama.create(model=model, modelfile=modelfile_content)
                if verbose:
                    print(f"Successfully created model '{model}'")
            except Exception as e:
                print(f"Failed to create model '{model}'. Error: {e}")
        else:
            if verbose:
                print(
                    f"Directory or modelfile for model '{model}' does not exist. Skipping."
                )


def serve_ollama(expose_external: bool, verbose: bool) -> None:
    """Starts the Ollama server with optional external exposure.

    Args:
        expose_external (bool): Flag to expose the Ollama server externally.
        verbose (bool): Flag indicating whether verbose mode is enabled.
    """
    os.environ["OLLAMA_HOST"] = (
        "0.0.0.0:11434" if expose_external else "127.0.0.1:11434"
    )
    host = os.environ["OLLAMA_HOST"]

    if verbose:
        print(f"Starting Ollama server on '{host}'...")

    try:
        ollama.serve()
        if verbose:
            print("Ollama server is now running.")
    except Exception as e:
        print(f"Failed to start Ollama server. Error: {e}")


def main() -> None:
    """Parse command-line arguments and execute model creation and server startup."""
    parser = argparse.ArgumentParser(
        description="Automate the creation and serving of multiple Ollama models.",
        epilog="Use this script to set up and start multiple models with the Ollama server.",
    )
    parser.add_argument(
        "--base_dir",
        type=str,
        default=DEFAULT_BASE_DIR,
        help=f"Base directory where model folders are located. Default is '{DEFAULT_BASE_DIR}'.",
    )
    parser.add_argument(
        "--expose_external",
        action="store_true",
        help="Expose the Ollama server externally (on '0.0.0.0:11434'). By default, the server runs on localhost.",
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="Enable verbose mode for detailed output.",
    )
    args = parser.parse_args()

    verbose = args.verbose

    # Create models
    create_models(args.base_dir, DEFAULT_MODELS, verbose)

    # Serve Ollama
    serve_ollama(args.expose_external, verbose)


if __name__ == "__main__":
    main()
