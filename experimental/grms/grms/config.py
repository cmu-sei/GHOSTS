"""Service configuration via environment variables."""

from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    host: str = "0.0.0.0"
    port: int = 8090
    debug: bool = False

    llm_source: str = "ollama"
    llm_model: str = "mistral:7b"
    # llm_model: str = "gemma4"
    llm_host: str = "http://host.docker.internal:11434"
    llm_temperature: float = 0.0
    llm_max_tokens: int = 4096

    llm_seed: int | None = None

    openai_api_key: str = ""
    aws_region: str = "us-east-1"
    bedrock_model_id: str = "anthropic.claude-3-sonnet-20240229-v1:0"

    db_path: str = "./data/grms_scoring.db"

    class Config:
        env_prefix = "GRMS_"
        env_file = ".env"


settings = Settings()
