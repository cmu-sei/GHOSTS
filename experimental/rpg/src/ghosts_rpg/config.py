"""Runtime configuration, sourced from the environment with sane defaults.

Mirrors GHOSTS conventions: OLLAMA_HOST / OLLAMA_MODEL match the API's
OllamaConnectorService. No model reachable -> the DM uses its deterministic
offline fallback (see DESIGN.md D2)."""

from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    # Live GHOSTS API (used by the loader when not loading a fixture export).
    ghosts_api_url: str = "http://localhost:5000"

    # Ollama DM brain. Empty model => offline deterministic fallback.
    ollama_host: str = "http://localhost:11434"
    ollama_model: str = ""

    # Where this service listens.
    host: str = "0.0.0.0"
    port: int = 8095

    @staticmethod
    def from_env() -> "Settings":
        return Settings(
            ghosts_api_url=os.getenv("GHOSTS_API_URL", "http://localhost:5000"),
            ollama_host=os.getenv("OLLAMA_HOST", "http://localhost:11434"),
            ollama_model=os.getenv("OLLAMA_MODEL", ""),
            host=os.getenv("GHOSTS_RPG_HOST", "0.0.0.0"),
            port=int(os.getenv("GHOSTS_RPG_PORT", "8095")),
        )
