"""LLM provider protocol and factory."""

from typing import Protocol

from grms.config import settings


class LLMProvider(Protocol):
    async def generate(self, system_prompt: str, user_prompt: str) -> str: ...


def get_provider() -> LLMProvider:
    match settings.llm_source:
        case "openai":
            from grms.llm.openai_provider import OpenAIProvider
            return OpenAIProvider()
        case "bedrock":
            from grms.llm.bedrock_provider import BedrockProvider
            return BedrockProvider()
        case _:
            from grms.llm.ollama_provider import OllamaProvider
            return OllamaProvider()
