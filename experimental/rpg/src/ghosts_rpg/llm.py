"""LLM clients for the judge/OPFOR brain, with a deterministic offline fallback.

Two providers, selected by Settings.llm_provider:
  - "ollama"  (default): POST {host}/api/generate, streaming NDJSON. Mirrors
    GHOSTS' OllamaConnectorService (env OLLAMA_HOST / OLLAMA_MODEL).
  - "bedrock": AWS Bedrock Anthropic Messages API via boto3.

Both expose the same contract the judge/OPFOR rely on — `enabled` and
`generate(prompt, system) -> str | None`. When the backend is unconfigured or
unreachable, `generate` returns None so the game falls back to its templated
path and stays playable with no model present."""

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
            timeout = httpx.Timeout(self.settings.ollama_timeout, connect=2.0)
            with httpx.Client(timeout=timeout) as client:
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


class BedrockClient:
    """AWS Bedrock via the Anthropic Messages API (boto3 bedrock-runtime).

    Credentials/region come from the standard AWS environment (AWS_REGION,
    AWS_ACCESS_KEY_ID, ...). boto3 is imported lazily so the Ollama path never
    depends on it."""

    def __init__(self, settings: Optional[Settings] = None):
        self.settings = settings or Settings.from_env()
        self._client = None  # lazy boto3 bedrock-runtime client

    @property
    def enabled(self) -> bool:
        return bool(self.settings.bedrock_model)

    def _runtime(self):
        if self._client is None:
            import boto3  # lazy: only needed for the bedrock provider

            self._client = boto3.client(
                "bedrock-runtime", region_name=self.settings.bedrock_region
            )
        return self._client

    def generate(self, prompt: str, system: Optional[str] = None) -> Optional[str]:
        """Return generated text, or None if disabled/unreachable (=> use fallback)."""
        if not self.enabled:
            return None
        body = {
            "anthropic_version": "bedrock-2023-05-31",
            "max_tokens": self.settings.bedrock_max_tokens,
            "messages": [{"role": "user", "content": prompt}],
        }
        if system:
            body["system"] = system
        try:
            resp = self._runtime().invoke_model(
                modelId=self.settings.bedrock_model,
                body=json.dumps(body),
            )
            payload = json.loads(resp["body"].read())
            parts = [
                block.get("text", "")
                for block in payload.get("content", [])
                if block.get("type") == "text"
            ]
            text = "".join(parts).strip()
            return text or None
        except Exception:
            # Any boto/credential/throttling error => offline fallback.
            return None


def make_llm(settings: Optional[Settings] = None):
    """Build the LLM client for the configured provider (default: Ollama)."""
    settings = settings or Settings.from_env()
    if settings.llm_provider == "bedrock":
        return BedrockClient(settings)
    return OllamaClient(settings)
