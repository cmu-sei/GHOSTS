import tempfile
from typing import Optional

from app_logging import setup_logger
from config.config import VOICE_MODEL, VOICE_SYNTHESIS_ENABLED
from fastapi import APIRouter, Query, Response
from fastapi.responses import FileResponse
from utils.ollama import generate_document_with_ollama
from utils.voice import generate_audio_response

logger = setup_logger(__name__)

router = APIRouter()


def filter_dialogue_lines(conversation_script: str) -> str:
    """Filter and return only lines that start with 'actor1:' or 'actor2:'."""
    logger.debug("Filtering dialogue lines from the generated conversation script.")
    lines = conversation_script.strip().split("\n")
    filtered_lines = [
        line
        for line in lines
        if line.startswith("actor1:") or line.startswith("actor2:")
    ]
    logger.debug("Filtered lines: %s", filtered_lines)
    return "\n".join(filtered_lines)


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
    """Generate and return a synthesized telephone conversation between two actors."""

    logger.debug("Received request to generate a synthesized conversation.")
    logger.debug("Actor1 role: %s, Actor2 role: %s", actor1, actor2)

    # Create a prompt for the conversation script
    prompt = (
        f"Create a professional conversation between a {actor1} and a {actor2} "
        "discussing a project update. The conversation should be realistic and cover "
        "concerns about deadlines and workload adjustments. Format the output as follows don't use the role to describe the actor use actor1 or actor2:\n\n"
        "actor1: [Dialogue for actor1]\n"
        "actor2: [Dialogue for actor2]\n"
        "actor1: [Dialogue for actor1]\n"
        "actor2: [Dialogue for actor2]\n"
        "...\n\n"
        "Only output the dialogue lines in this format. Do NOT include any setting descriptions, "
        "character names, or additional context—just the dialogue lines."
    )

    logger.debug("Generated prompt for ChatOllama: %s", prompt)

    # Generate the conversation script using ChatOllama
    conversation_script = generate_document_with_ollama(prompt, model=VOICE_MODEL)

    if not conversation_script:
        logger.warning("Failed to generate conversation script using ChatOllama.")
        return Response(
            content="Failed to generate conversation script.", media_type="text/plain"
        )

    logger.info("Generated conversation script: %s", conversation_script)

    # Filter out any extraneous content to ensure only dialogue lines are used
    filtered_script = filter_dialogue_lines(conversation_script)
    logger.debug("Filtered conversation script: %s", filtered_script)

    if VOICE_SYNTHESIS_ENABLED:
        logger.info("Synthesis enabled. Generating audio for both voices.")

        # Generate audio using the filtered dialogue script
        audio_data = generate_audio_response(filtered_script)

    if audio_data:
        # Create a temporary file to store the audio data
        with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp_file:
            tmp_file.write(audio_data)
            tmp_file.close()

            # Return the audio file as a response
            logger.debug(f"Returning audio file: {tmp_file.name}")
            return FileResponse(tmp_file.name, media_type="audio/wav")

    else:
        logger.warning("Failed to generate audio data. Check synthesis service.")
        return Response(content="Audio synthesis failed.", media_type="text/plain")
