import io
import time

import torch
from app_logging import setup_logger
from config.config import DIFFUSERS_LOCAL_FILES_ONLY, IMAGE_GENERATION_MODEL
from diffusers import AutoPipelineForText2Image

logger = setup_logger(__name__)


def generate_image_with_diffusers(
    prompt: str,
    guidance_scale=0,
    height=512,
    width=512,
    num_inference_steps=1,
    seed=None,
    image_format="PNG",
):
    """Generate an image using the diffusion model and return it as bytes (no saving to local storage)."""
    logger.info("Starting image generation request.")

    try:
        # Log input parameters
        logger.debug(
            f"Input parameters - Prompt: '{prompt}', Guidance Scale: {guidance_scale}, "
            f"Height: {height}, Width: {width}, Steps: {num_inference_steps}, Seed: {seed}, "
            f"Image Format: {image_format}"
        )

        # Determine device
        device = "cuda" if torch.cuda.is_available() else "cpu"
        logger.info(f"Using device: {device}")

        # Record model loading time
        start_time = time.time()
        logger.debug(f"Loading Diffusion model: {IMAGE_GENERATION_MODEL}")

        pipeline = AutoPipelineForText2Image.from_pretrained(
            IMAGE_GENERATION_MODEL,
            torch_dtype=torch.float16 if device == "cuda" else torch.float32,
            variant="fp16",
            local_files_only=DIFFUSERS_LOCAL_FILES_ONLY,
        )
        pipeline.to(device)
        logger.info(f"Model loaded in {time.time() - start_time:.2f} seconds.")

        # Apply memory optimisation
        pipeline.enable_attention_slicing()
        logger.debug("Enabled attention slicing for memory optimisation.")

        # Set seed for reproducibility if provided
        generator = None
        if seed is not None:
            generator = torch.manual_seed(seed)
            logger.info(f"Seed set for reproducibility: {seed}")

        # Log inference start
        logger.info(
            f"Starting image generation - Prompt: '{prompt}', Height: {height}, Width: {width}, "
            f"Guidance Scale: {guidance_scale}, Steps: {num_inference_steps}"
        )

        # Record inference start time
        start_inference_time = time.time()

        # Generate the image
        result = pipeline(
            prompt=prompt,
            height=height,
            width=width,
            guidance_scale=guidance_scale,
            num_inference_steps=num_inference_steps,
            generator=generator,
        )
        image = result.images[0]
        logger.info(
            f"Image generated successfully in {time.time() - start_inference_time:.2f} seconds."
        )

        # Convert the image to bytes (no saving to file)
        logger.debug("Converting image to byte format.")
        image_bytes = io.BytesIO()
        image.save(image_bytes, format=image_format.upper())
        image_bytes.seek(0)
        logger.info(f"Image successfully converted to {image_format.upper()} format.")

        return image_bytes

    except Exception as e:
        logger.error(f"Error during image generation: {e}", exc_info=True)
        return None
