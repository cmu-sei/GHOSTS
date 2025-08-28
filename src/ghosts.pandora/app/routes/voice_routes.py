import tempfile
from typing import Optional

from app_logging import setup_logger
from config.config import VOICE_GENERATION_ENABLED, VOICE_MODEL
from fastapi import APIRouter, Query, Request, Response
from fastapi.responses import FileResponse
from utils.ollama import generate_document_with_ollama
from utils.voice import generate_audio_response

router = APIRouter()
logger = setup_logger(__name__)


def _filter_dialogue(script: str) -> str:
    return "\n".join(
        line for line in script.strip().splitlines()
        if line.startswith("actor1:") or line.startswith("actor2:")
    )


def _temp_audio(audio_data: bytes) -> str:
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp:
        tmp.write(audio_data)
        return tmp.name


def generate_synthesised_conversation(
    request: Request,
    actor1: Optional[str] = Query("software engineer", description="Voice 1 role"),
    actor2: Optional[str] = Query("senior software engineer", description="Voice 2 role"),
) -> Response:
    logger.debug(f"Actors: {actor1} vs {actor2}")
    prompt = (
        f"Create a professional conversation between a {actor1} and a {actor2} "
        "discussing a project update. Format:\n\n"
        "actor1: [Line]\nactor2: [Line]\n...\n\n"
        "Only dialogue. No setting, names, or context."
    )

    script = generate_document_with_ollama(prompt, model=VOICE_MODEL)
    if not script:
        logger.warning("Script generation failed.")
        return Response(content="Failed to generate conversation script.", media_type="text/plain")

    filtered = _filter_dialogue(script)
    logger.debug(f"Filtered script:\n{filtered}")

    if not VOICE_GENERATION_ENABLED:
        logger.info("Voice synthesis disabled. Returning text.")
        return Response(content=filtered, media_type="text/plain")

    logger.info("Generating voice from script.")
    audio = generate_audio_response(filtered)
    if audio:
        path = _temp_audio(audio)
        return FileResponse(path, media_type="audio/wav")

    logger.error("Audio generation failed.")
    return Response(content="Audio synthesis failed.", media_type="text/plain")


# route registration
ROUTES = ["/call", "/calls"]
for route in ROUTES:
    router.add_api_route(f"{route}", generate_synthesised_conversation, methods=["GET", "POST"], tags=["Audio"])
    router.add_api_route(f"{route}/{{file_name:path}}", generate_synthesised_conversation, methods=["GET", "POST"], tags=["Audio"])
