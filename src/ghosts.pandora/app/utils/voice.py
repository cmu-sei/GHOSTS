import os
import tempfile
from typing import Optional

import pyttsx3
from app_logging import setup_logger

# Set up logging
logger = setup_logger(__name__)


def generate_audio_response(conversation_script: str) -> Optional[bytes]:
    """Generate audio from a conversation script using pyttsx3 with two different voices.

    Args:
        conversation_script (str): The text of the conversation to be synthesized.

    Returns:
        Optional[bytes]: The synthesized audio in a binary format, or None if synthesis fails.
    """
    try:
        # Initialize the pyttsx3 engine
        logger.debug("Initializing pyttsx3 engine.")
        engine = pyttsx3.init(driverName="espeak")

        # Get the available voices
        voices = engine.getProperty("voices")
        logger.debug(f"Available voices retrieved: {len(voices)}")

        # Assign voices for actor1 and actor2
        actor1_voice_id = voices[0].id if voices else None
        actor2_voice_id = voices[1].id if len(voices) > 1 else actor1_voice_id
        logger.debug(f"Actor1 voice ID: {actor1_voice_id}")
        logger.debug(f"Actor2 voice ID: {actor2_voice_id}")

        # Check if at least one voice is available
        if not actor1_voice_id:
            logger.error("No voices available for audio synthesis.")
            return None

        # Create a temporary file to save the audio
        with tempfile.NamedTemporaryFile(
            delete=False, suffix=".wav"
        ) as temp_audio_file:
            temp_audio_path = temp_audio_file.name
        logger.debug(f"Temporary file created for audio: {temp_audio_path}")

        # Process the conversation script
        lines = conversation_script.strip().split("\n")
        logger.debug(f"Processing {len(lines)} lines of dialogue for synthesis.")

        # Synthesize each line of the conversation script
        for line in lines:
            if line.startswith("actor1:"):
                dialogue = line.replace("actor1:", "").strip()
                engine.setProperty("voice", actor1_voice_id)
                logger.debug(f"Actor1 dialogue: {dialogue}")
            elif line.startswith("actor2:"):
                dialogue = line.replace("actor2:", "").strip()
                engine.setProperty("voice", actor2_voice_id)
                logger.debug(f"Actor2 dialogue: {dialogue}")
            else:
                logger.warning(f"Unexpected line format: {line}")
                continue

            # Save the dialogue to the temporary file
            if dialogue:
                logger.debug(f"Synthesizing and saving dialogue: {dialogue}")
                engine.save_to_file(dialogue, temp_audio_path)
                engine.runAndWait()

        # Stop the engine after processing
        engine.stop()
        logger.debug("pyttsx3 engine finished processing.")

        # Read the audio data from the temporary file
        if os.path.exists(temp_audio_path):
            logger.debug(f"Reading audio data from file: {temp_audio_path}")
            with open(temp_audio_path, "rb") as audio_file:
                audio_data = audio_file.read()
            logger.debug("Audio data read successfully.")
        else:
            logger.error(f"Temporary audio file not found: {temp_audio_path}")
            return None

        # Clean up the temporary file
        os.remove(temp_audio_path)
        logger.debug(f"Temporary audio file deleted: {temp_audio_path}")

        return audio_data

    except Exception as e:
        logger.error(f"Error during audio synthesis: {e}", exc_info=True)
        return None
