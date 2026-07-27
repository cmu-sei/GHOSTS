"""Runtime configuration, sourced from the environment with sane defaults.

Mirrors GHOSTS conventions: OLLAMA_HOST / OLLAMA_MODEL match the API's
OllamaConnectorService. No model reachable -> the DM uses its deterministic
offline fallback."""

from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    # Live GHOSTS API (used by the loader when not loading a fixture export).
    ghosts_api_url: str = "http://localhost:5000"

    # LLM provider for the judge/OPFOR brain: "ollama" (default) or "bedrock".
    # Either way, an unreachable/unconfigured backend => offline deterministic fallback.
    llm_provider: str = "ollama"

    # Ollama DM brain. Empty model => offline deterministic fallback.
    ollama_host: str = "http://localhost:11434"
    ollama_model: str = ""
    # Read timeout (seconds) for a single generation. Reasoning models on modest
    # hardware can take minutes per call; generous by default so long turns
    # complete instead of silently falling back to the offline path.
    ollama_timeout: float = 300.0

    # AWS Bedrock DM brain (used when llm_provider == "bedrock").
    bedrock_model: str = "us.anthropic.claude-opus-4-8"
    bedrock_region: str = "us-east-1"
    bedrock_max_tokens: int = 1024

    # Where this service listens.
    host: str = "0.0.0.0"
    port: int = 8095

    @staticmethod
    def from_env() -> "Settings":
        return Settings(
            ghosts_api_url=os.getenv("GHOSTS_API_URL", "http://localhost:5000"),
            llm_provider=os.getenv("RPG_LLM_PROVIDER", "ollama").strip().lower(),
            ollama_host=os.getenv("OLLAMA_HOST", "http://localhost:11434"),
            ollama_model=os.getenv("OLLAMA_MODEL", ""),
            ollama_timeout=float(os.getenv("OLLAMA_TIMEOUT", "300")),
            bedrock_model=os.getenv("BEDROCK_MODEL", "us.anthropic.claude-opus-4-8"),
            bedrock_region=os.getenv("AWS_REGION", "us-east-1"),
            bedrock_max_tokens=int(os.getenv("BEDROCK_MAX_TOKENS", "1024")),
            host=os.getenv("GHOSTS_RPG_HOST", "0.0.0.0"),
            port=int(os.getenv("GHOSTS_RPG_PORT", "8095")),
        )
