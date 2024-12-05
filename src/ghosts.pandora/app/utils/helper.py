import random
import tarfile
from io import BytesIO
from typing import Generator, List, Tuple

import cv2
import numpy as np
import pyzipper
import zipstream
from app_logging import setup_logger
from config.config import HTML_MODEL, JSON_MODEL, OLLAMA_ENABLED, TEXT_MODEL
from faker import Faker
from fastapi import Response
from fastapi.responses import StreamingResponse
from PIL import Image, ImageDraw, ImageFont
from utils.ollama import generate_document_with_ollama

logger = setup_logger(__name__)
fake = Faker()


def generate_random_name(extension: str = "") -> str:
    """
    Generate a random name with the specified file extension.

    Args:
        extension (str): The file extension to append to the name.

    Returns:
        str: A randomly generated file name with the given extension.
    """
    name = f"{fake.word()}_{fake.word()}{extension}"
    logger.debug(f"Generated random name: {name}")
    return name


def generate_frames() -> Generator[bytes, None, None]:
    """
    Generate random video frames as JPEG.

    Yields:
        Generator[bytes, None, None]: A stream of JPEG-encoded video frames.
    """
    while True:
        frame = np.random.randint(0, 256, (480, 640, 3), dtype=np.uint8)
        ret, buffer = cv2.imencode(".jpg", frame)
        if not ret:
            logger.warning("Failed to encode frame as JPEG.")
            continue
        logger.debug(f"Generated frame of size: {buffer.size} bytes.")
        yield (
            b"--frame\r\n"
            b"Content-Type: image/jpeg\r\n\r\n" + buffer.tobytes() + b"\r\n"
        )


def generate_zip_stream(zip_file: zipstream.ZipFile) -> Generator[bytes, None, None]:
    """
    Yield chunks from the ZIP file for streaming.

    Args:
        zip_file (zipstream.ZipFile): A zipstream.ZipFile object containing the files.

    Yields:
        Generator[bytes, None, None]: Chunks of the ZIP file content.
    """
    for chunk in zip_file:
        logger.debug(f"Yielding chunk of size: {len(chunk)} bytes.")
        yield chunk


def generate_video_from_frames(
    output_file: str, frame_count: int = 100, fps: int = 30
) -> None:
    """
    Create a video using randomly generated frames.

    Args:
        output_file (str): The file path for the output video.
        frame_count (int): Number of frames to generate. Defaults to 100.
        fps (int): Frames per second for the video. Defaults to 30.
    """
    logger.info(
        f"Generating video '{output_file}' with {frame_count} frames at {fps} FPS."
    )

    frame_generator = generate_frames()
    frame_width, frame_height = 640, 480
    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
    out = cv2.VideoWriter(output_file, fourcc, fps, (frame_width, frame_height))

    for i in range(frame_count):
        frame = next(frame_generator)
        frame_data = frame.split(b"\r\n\r\n")[1]
        img = cv2.imdecode(np.frombuffer(frame_data, dtype=np.uint8), cv2.IMREAD_COLOR)
        out.write(img)
        logger.debug(f"Written frame {i + 1}/{frame_count} to video.")

    out.release()
    logger.info(f"Video generation complete: {output_file}")


def generate_image_response(request_type: str) -> Response:
    """
    Generate a random image response based on the requested format.

    Args:
        request_type (str): The image format (e.g., 'jpg', 'png').

    Returns:
        Response: A FastAPI response containing the generated image.
    """
    height, width = random.randint(200, 800), random.randint(200, 800)
    content_type = f"image/{request_type}"

    img = Image.new(mode="RGB", size=(width, height), color=(255, 255, 255))
    draw = ImageDraw.Draw(img)

    for _ in range(random.randint(5, 15)):
        shape_type = random.choice(["circle", "rectangle"])
        color = tuple(random.randint(0, 255) for _ in range(3))

        x1, y1 = random.randint(0, width - 1), random.randint(0, height - 1)
        x2, y2 = random.randint(x1 + 1, width), random.randint(y1 + 1, height)

        if shape_type == "circle":
            radius = random.randint(10, 100)
            draw.ellipse((x1, y1, x1 + radius, y1 + radius), fill=color)
        else:
            draw.rectangle((x1, y1, x2, y2), fill=color)

    text = fake.sentence(nb_words=random.randint(2, 6))
    font_size = random.randint(20, 50)
    font_color = tuple(random.randint(0, 255) for _ in range(3))
    font = ImageFont.load_default()

    text_x = random.randint(0, max(0, width - 100))
    text_y = random.randint(0, max(0, height - font_size))
    draw.text((text_x, text_y), text, fill=font_color, font=font)

    with BytesIO() as buf:
        img_format = "JPEG" if request_type == "jpg" else request_type.upper()
        img.save(buf, img_format)
        buf.seek(0)

        file_name = generate_random_name(f".{request_type}")
        logger.info(f"Generated image: {file_name} ({img_format}, {width}x{height})")

        response = Response(content=buf.read(), media_type=content_type)
        response.headers["Content-Disposition"] = f"attachment; filename={file_name}"
        return response


def create_random_files(num_files: int) -> List[Tuple[str, bytes]]:
    """
    Generate random file names and content for specified extensions.

    Args:
        num_files (int): Number of files to generate.

    Returns:
        List[Tuple[str, bytes]]: A list of tuples containing filenames and content.
    """
    files = []
    for _ in range(num_files):
        extension = random.choice([".txt", ".html", ".json"])
        file_name = generate_random_name(extension)

        if OLLAMA_ENABLED:
            prompt = f"Generate a document suitable for a {extension} file format."
            logger.info(f"Requesting content for '{file_name}' from Ollama.")
            if extension == ".txt":
                model = TEXT_MODEL
            elif extension == ".html":
                model = HTML_MODEL
            elif extension == ".json":
                model = JSON_MODEL
            else:
                break

            content = generate_document_with_ollama(prompt, model, timeout=3)
            if content:
                files.append((file_name, content.encode("utf8")))
                logger.info(f"Generated content for {file_name} using Ollama.")
                continue

        # Fallback to Faker for content generation
        logger.info(f"Falling back to Faker for {file_name}.")
        if extension == ".txt":
            content = fake.text(max_nb_chars=500)
        elif extension == ".json":
            content = fake.json(
                data_columns={"name": "name", "email": "email"}, num_rows=3
            )
        elif extension == ".html":
            content = f"<h1>{fake.sentence()}</h1><p>{fake.paragraph()}</p>"
        else:
            content = fake.text(max_nb_chars=300)

        files.append((file_name, content.encode("utf8")))
        logger.info(f"Generated random content for {file_name}.")
    return files


def generate_archive(file_name: str, archive_type: str) -> BytesIO:
    """
    Generate a compressed archive (ZIP or TAR) containing files with random content.

    Args:
        file_name (str): Name of the output archive.
        archive_type (str): Type of archive ("zip" or "tar").

    Returns:
        BytesIO: A buffer containing the archive data.
    """
    buffer = BytesIO()
    num_files = random.randint(5, 15)  # Generate 5-15 files
    files = create_random_files(num_files)

    if archive_type == "zip":
        with pyzipper.ZipFile(buffer, "w", pyzipper.ZIP_DEFLATED) as archive:
            for name, content in files:
                archive.writestr(name, content)
                logger.info(f"Added {name} to ZIP archive.")
    elif archive_type == "tar":
        with tarfile.open(fileobj=buffer, mode="w") as archive:
            for name, content in files:
                tar_info = tarfile.TarInfo(name=name)
                tar_info.size = len(content)
                archive.addfile(tar_info, BytesIO(content))
                logger.info(f"Added {name} to TAR archive.")
    else:
        logger.error(f"Unsupported archive type: {archive_type}")
        raise ValueError(f"Unsupported archive type: {archive_type}")

    buffer.seek(0)
    logger.info(f"{archive_type.upper()} archive created: {file_name}")
    return buffer


def create_response(
    buffer: BytesIO, file_name: str, media_type: str
) -> StreamingResponse:
    """
    Helper function to create a streaming response.

    Args:
        buffer (BytesIO): The buffer containing the content.
        file_name (str): Name of the file to attach to the response.
        media_type (str): The MIME type of the response.

    Returns:
        StreamingResponse: A FastAPI streaming response.
    """
    logger.debug(f"Creating streaming response for file: {file_name}")
    return StreamingResponse(
        buffer,
        media_type=media_type,
        headers={"Content-Disposition": f"attachment; filename={file_name}"},
    )


def clean_content(content: str) -> str:
    """
    Remove backticks and the word 'html' from the start or end of the content.

    Args:
        content (str): The raw content to clean.

    Returns:
        str: Cleaned content with unnecessary characters removed.
    """
    content = content.replace("```html", "").strip()
    content = content.replace("```", "").strip()

    return content
