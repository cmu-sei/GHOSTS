# GHOSTS GRMS (Geopolitical Response Modeling Service)

GRMS is a FastAPI service (Geopolitical Response Modeling Service). Here's how to run it:

  Install and run locally:

  cd /workspaces/GHOSTS/services/grms

  # Install the package in editable mode
  pip install -e .
  pip install -r requirements.txt

  # Run the service
  uvicorn grms.main:app --host 0.0.0.0 --port 8090

  Or via Docker:

  cd /workspaces/GHOSTS/services/grms
  docker build -t grms .
  docker run -p 8090:8090 grms

  Key config (set via env vars prefixed GRMS_ or a .env file):

  ┌─────────────────────┬───────────────────────────────────┬──────────────────────────────────┐
  │      Variable       │              Default              │              Notes               │
  ├─────────────────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ GRMS_LLM_SOURCE     │ ollama                            │ ollama, openai, or bedrock       │
  ├─────────────────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ GRMS_LLM_MODEL      │ mistral:7b                        │ Model name for the chosen source │
  ├─────────────────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ GRMS_LLM_HOST       │ http://host.docker.internal:11434 │ Ollama endpoint                  │
  ├─────────────────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ GRMS_OPENAI_API_KEY │ (empty)                           │ Required if using OpenAI         │
  ├─────────────────────┼───────────────────────────────────┼──────────────────────────────────┤
  │ GRMS_PORT           │ 8090                              │ Listening port                   │
  └─────────────────────┴───────────────────────────────────┴──────────────────────────────────┘

  Once running, the API is at http://localhost:8090. FastAPI auto-docs are at http://localhost:8090/docs.
