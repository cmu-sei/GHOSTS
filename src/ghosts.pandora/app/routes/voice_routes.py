import tempfile
from typing import Optional

from app_logging import setup_logger
from config.config import VOICE_GENERATION_ENABLED, VOICE_MODEL
from fastapi import APIRouter, Query, Response
from fastapi.responses import FileResponse
from utils.ollama import generate_document_with_ollama
from utils.voice import generate_audio_response

logger = setup_logger(__name__)
router = APIRouter()


def filter_dialogue_lines(script: str) -> str:
    """Extract and return only lines starting with 'actor1:' or 'actor2:'."""
    logger.debug("Filtering dialogue lines from the script.")
    return "\n".join(
        line
        for line in script.strip().splitlines()
        if line.startswith("actor1:") or line.startswith("actor2:")
    )


def create_temp_audio_file(audio_data: bytes) -> str:
    """Save audio data to a temporary file and return the file path."""
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp_file:
        tmp_file.write(audio_data)
        return tmp_file.name


@router.get("/call", tags=["Audio"])
@router.post("/call", tags=["Audio"])
def generate_synthesised_conversation(
    actor1: Optional[str] = Query(
        "software engineer", description="Role of the first voice"
    ),
    actor2: Optional[str] = Query(
        "senior software engineer", description="Role of the second voice"
    ),
) -> Response:
    """
    Generate and return a synthesized telephone conversation between two actors.
    """

    logger.debug(f"Request received with actor1: {actor1}, actor2: {actor2}")

    # Create a prompt for the conversation script
    prompt = (
        f"Create a professional conversation between a {actor1} and a {actor2} "
        "discussing a project update. The conversation should be realistic and cover "
        "concerns about deadlines and workload adjustments. Format the output as follows:\n\n"
        "actor1: [Dialogue for actor1]\n"
        "actor2: [Dialogue for actor2]\n"
        "actor1: [Dialogue for actor1]\n"
        "actor2: [Dialogue for actor2]\n"
        "...\n\n"
        "Only output the dialogue lines in this format. Do NOT include any setting descriptions, "
        "character names, or additional context—just the dialogue lines."
    )

    logger.debug("Generated prompt for ChatOllama.")
    conversation_script = generate_document_with_ollama(prompt, model=VOICE_MODEL)

    if not conversation_script:
        logger.warning("Failed to generate conversation script.")
        return Response(
            content="Failed to generate conversation script.", media_type="text/plain"
        )

    logger.info("Conversation script generated successfully.")
    filtered_script = filter_dialogue_lines(conversation_script)
    logger.debug(
        f"Filtered script: {filtered_script}",
    )

    if not VOICE_GENERATION_ENABLED:
        logger.info("Voice generation is disabled. Returning text response.")
        return Response(content=filtered_script, media_type="text/plain")

    logger.info("Voice generation enabled. Generating audio.")
    audio_data = generate_audio_response(filtered_script)

    if audio_data:
        audio_file_path = create_temp_audio_file(audio_data)
        logger.debug(f"Returning generated audio file: {audio_file_path}")
        return FileResponse(audio_file_path, media_type="audio/wav")

    logger.error("Audio synthesis failed.")
    return Response(content="Audio synthesis failed.", media_type="text/plain")
