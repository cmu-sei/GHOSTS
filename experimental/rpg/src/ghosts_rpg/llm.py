"""Ollama client with a deterministic offline fallback.

Mirrors GHOSTS' OllamaConnectorService: POST {host}/api/generate, streaming NDJSON,
env OLLAMA_HOST / OLLAMA_MODEL. When no model is configured (or the host is
unreachable), `generate` returns None so the DM falls back to its templated path.
The whole game stays playable with no model present (DESIGN.md D2)."""

from __future__ import annotations

import json
from typing import Optional

import httpx

from .config import Settings


class OllamaClient:
    def __init__(self, settings: Optional[Settings] = None):
        self.settings = settings or Settings.from_env()

    @property
    def enabled(self) -> bool:
        return bool(self.settings.ollama_model)

    def generate(self, prompt: str, system: Optional[str] = None) -> Optional[str]:
        """Return generated text, or None if disabled/unreachable (=> use fallback)."""
        if not self.enabled:
            return None
        url = f"{self.settings.ollama_host.rstrip('/')}/api/generate"
        payload = {"model": self.settings.ollama_model, "prompt": prompt}
        if system:
            payload["system"] = system
        try:
            with httpx.Client(timeout=60.0) as client:
                resp = client.post(url, json=payload)
                resp.raise_for_status()
                out: list[str] = []
                for line in resp.text.splitlines():
                    line = line.strip()
                    if not line:
                        continue
                    try:
                        obj = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if obj.get("response"):
                        out.append(obj["response"])
                text = "".join(out).strip()
                return text or None
        except (httpx.HTTPError, OSError):
            return None
