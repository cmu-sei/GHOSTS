"""AWS Bedrock LLM provider."""

import json

import boto3

from grms.config import settings


class BedrockProvider:
    def __init__(self):
        self.client = boto3.client("bedrock-runtime", region_name=settings.aws_region)
        self.model_id = settings.bedrock_model_id

    async def generate(self, system_prompt: str, user_prompt: str) -> str:
        body = json.dumps({
            "anthropic_version": "bedrock-2023-05-31",
            "max_tokens": settings.llm_max_tokens,
            "temperature": settings.llm_temperature,
            "system": system_prompt,
            "messages": [{"role": "user", "content": user_prompt}],
        })
        response = self.client.invoke_model(modelId=self.model_id, body=body)
        result = json.loads(response["body"].read())
        return result["content"][0]["text"]
