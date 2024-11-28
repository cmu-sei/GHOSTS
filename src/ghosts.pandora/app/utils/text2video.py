"""WIP Requires Tuning (Works with a single H100 or A100 currently)"""

import os

import numpy as np
import torch
from app_logging import setup_logger
from diffusers import (AutoencoderKLCogVideoX, CogVideoXPipeline,
                       CogVideoXTransformer3DModel)
from diffusers.utils import export_to_video
from torch.amp import autocast
from torchao.quantization import int8_weight_only, quantize_
from transformers import T5EncoderModel

os.environ["PYTORCH_CUDA_ALLOC_CONF"] = "expandable_segments:True"
torch._dynamo.config.suppress_errors = True


logger = setup_logger(__name__)


def generate_video_with_cogvideox(
    prompt: str,
    num_inference_steps: int = 50,
    num_frames: int = 49,
    guidance_scale: float = 6.0,
    seed: int = 42,
    output_filename: str = "output.mp4",
    fps: int = 8,
):
    """
    Generate a video based on a text prompt using CogVideoX with optimised memory usage.

    Args:
        prompt (str): The text description of the video content.
        num_inference_steps (int): Number of diffusion steps.
        num_frames (int): Total number of frames in the generated video.
        guidance_scale (float): Scale of guidance for the prompt.
        seed (int): Random seed for reproducibility.
        output_filename (str): Output filename for the video.
        fps (int): Frames per second for the video.

    Returns:
        str: Path to the generated video.
    """
    logger.info("Starting video generation process.")

    try:
        # Determine device
        device = "cuda" if torch.cuda.is_available() else "cpu"
        logger.info(f"Using device: {device}")

        # Log prompt and parameters
        logger.debug(
            f"Parameters - Prompt: '{prompt}', Steps: {num_inference_steps}, Frames: {num_frames}, "
            f"Guidance Scale: {guidance_scale}, Seed: {seed}, FPS: {fps}, Output: {output_filename}"
        )

        # Load and quantise submodules
        logger.info("Loading and quantising model components for memory efficiency.")

        quantization = int8_weight_only

        # Load and apply auto-tuning to text encoder
        logger.debug("Loading text encoder with auto-tuning...")
        text_encoder = T5EncoderModel.from_pretrained(
            "THUDM/CogVideoX-5b", subfolder="text_encoder", torch_dtype=torch.float16
        )
        # Apply auto-tuning (compile and quantize)
        text_encoder = torch.compile(text_encoder, mode="max-autotune")
        quantize_(text_encoder, quantization(), device=device)
        logger.debug("Text encoder loaded, auto-tuned and quantised.")

        # Load and apply auto-tuning to transformer
        logger.debug("Loading transformer with auto-tuning...")
        transformer = CogVideoXTransformer3DModel.from_pretrained(
            "THUDM/CogVideoX-5b", subfolder="transformer", torch_dtype=torch.float16
        )
        # Apply auto-tuning (compile and quantize)
        transformer = torch.compile(transformer, mode="max-autotune")
        quantize_(transformer, quantization(), device=device)
        logger.debug("Transformer loaded, auto-tuned and quantised.")

        # Load and apply auto-tuning to VAE
        logger.debug("Loading VAE with auto-tuning...")
        vae = AutoencoderKLCogVideoX.from_pretrained(
            "THUDM/CogVideoX-2b", subfolder="vae", torch_dtype=torch.float16
        )
        # Apply auto-tuning (compile and quantize)
        vae = torch.compile(vae, mode="max-autotune")
        quantize_(vae, quantization(), device=device)
        logger.debug("VAE loaded, auto-tuned and quantised.")

        # Create pipeline with auto-tuned and quantized components
        logger.info("Creating pipeline with optimised components.")
        pipe = CogVideoXPipeline.from_pretrained(
            "THUDM/CogVideoX-2b",
            text_encoder=text_encoder,
            transformer=transformer,
            vae=vae,
            torch_dtype=torch.float16,
        )

        # Enable memory optimisations
        # pipe.enable_model_cpu_offload()
        logger.debug("Model CPU offloading enabled.")
        # pipe.enable_sequential_cpu_offload()
        pipe.vae.enable_slicing()
        pipe.vae.enable_tiling()
        logger.debug("VAE tiling enabled for memory optimisation.")

        # Set seed for reproducibility
        logger.info("Setting random seed for reproducibility.")
        generator = torch.Generator(device=device).manual_seed(seed)

        # Generate video
        logger.info("Starting video generation process.")

        with autocast("cuda", enabled=True):
            video_frames = pipe(
                prompt=prompt,
                num_videos_per_prompt=1,
                num_inference_steps=num_inference_steps,
                num_frames=num_frames,
                guidance_scale=guidance_scale,
                generator=generator,
            ).frames[0]

        logger.info("Video frames generated successfully.")

        # Clip and scale image values
        video_frames = [np.clip(frame, 0, 1) for frame in video_frames]
        video_frames = [(frame * 255).round().astype("uint8") for frame in video_frames]

        # Export video to file
        logger.debug("Exporting video frames to file.")
        export_to_video(video_frames, output_filename, fps=fps)
        logger.info(f"Video generated and saved to {output_filename}")

        return output_filename

    except Exception as e:
        logger.error(f"Error during video generation: {e}", exc_info=True)
        return None
