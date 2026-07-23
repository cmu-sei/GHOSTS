"""Ollama LLM provider."""

import httpx

from grms.config import settings


class OllamaProvider:
    def __init__(self):
        self.host = settings.llm_host
        self.model = settings.llm_model

    async def generate(self, system_prompt: str, user_prompt: str) -> str:
        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            "stream": False,
            "options": {
                "temperature": settings.llm_temperature,
                "num_predict": settings.llm_max_tokens,
                **({"seed": settings.llm_seed} if settings.llm_seed is not None else {}),
            },
        }
        async with httpx.AsyncClient(timeout=120.0) as client:
            resp = await client.post(f"{self.host}/api/chat", json=payload)
            resp.raise_for_status()
            data = resp.json()
            return data["message"]["content"]
