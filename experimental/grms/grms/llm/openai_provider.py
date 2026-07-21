"""OpenAI LLM provider."""

from openai import AsyncOpenAI

from grms.config import settings


class OpenAIProvider:
    def __init__(self):
        self.client = AsyncOpenAI(api_key=settings.openai_api_key)
        self.model = settings.llm_model if not settings.llm_model.startswith("mistral") else "gpt-4o"

    async def generate(self, system_prompt: str, user_prompt: str) -> str:
        response = await self.client.chat.completions.create(
            model=self.model,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            temperature=settings.llm_temperature,
            max_tokens=settings.llm_max_tokens,
            **({"seed": settings.llm_seed} if settings.llm_seed is not None else {}),
        )
        return response.choices[0].message.content or ""
