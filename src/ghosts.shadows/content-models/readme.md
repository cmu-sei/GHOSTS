## GHOSTS Shadows - Model Setup

The GHOSTS Shadows server uses multiple models in `ollama` to provide content to various applications. This README covers both a Python script method and a manual method for setting up and serving multiple models.

### Prerequisites

- Install Python and `ollama-python` library.
- Ensure each model directory (e.g., `content-models/activity`) includes a `modelfile` with the model definition.

### Available Models

Models in this setup include:
- `activity`
- `chat`
- `social`
- `excel_content`
- `web_content`
- `img_content`
- `lessons`

Each model is created based on its `modelfile`.

### Running the Server Using the Script

This script automates the model creation and serving process.

#### Usage

1. **Clone or save the script**: Save the provided script as `start_ollama_models.py`.
2. **Run the script**:

```bash
python start_ollama_models.py --verbose
```

This will:
- Create the models from `content-models` directory.
- Start the `ollama` server on `localhost:11434`.

**To expose the server externally** (on `0.0.0.0`), run:

```bash
python start_ollama_models.py --expose_external --verbose
```

#### Arguments

- `--base_dir`: Specify a custom base directory for models (default: `content-models`).
- `--expose_external`: Allow external access to the server.
- `--verbose`: Enable detailed output logging.

### Running the Server Manually

1. **Create Models**:
   - Manually navigate to each model directory and run the `ollama create` command.

```bash
cd content-models/activity
ollama create activity

cd content-models/chat
ollama create chat

cd content-models/social
ollama create social

cd content-models/excel_content
ollama create excel_content

cd content-models/web_content
ollama create web_content

cd content-models/img_content
ollama create img_content

cd content-models/lessons
ollama create lessons
```

2. **Serve Models**:
   - Start the `ollama` server with either of the following commands:

For **localhost only**:

```bash
ollama serve
```

For **external access**:

```bash
OLLAMA_HOST=0.0.0.0:11434 ollama serve
```
