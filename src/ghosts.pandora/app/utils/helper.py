# app/utils/helpers.py
from faker import Faker
import cv2
import numpy as np
from io import BytesIO
import zipstream
import random
from typing import Generator
import app_logging
from fastapi.responses import StreamingResponse
import pyzipper
import tarfile

from fastapi import Response
from PIL import Image, ImageDraw, ImageFont

fake = Faker()

logger = app_logging.setup_logger("app_logger")


def generate_random_name(extension: str = "") -> str:
    """Generate a random name with the specified file extension."""
    name = f"{fake.word()}_{fake.word()}{extension}"
    logger.debug("Generated random name: %s", name)
    return name


def generate_frames() -> Generator[bytes, None, None]:
    """Generate random video frames."""
    while True:
        # Generate a random image frame
        frame = np.random.randint(0, 256, (480, 640, 3), dtype=np.uint8)
        # Encode the frame as JPEG
        ret, buffer = cv2.imencode(".jpg", frame)
        if not ret:
            logger.warning("Failed to encode frame as JPEG")
            continue
        logger.debug("Generated a frame of size: %d bytes", buffer.size)
        yield (
            b"--frame\r\n"
            b"Content-Type: image/jpeg\r\n\r\n" + buffer.tobytes() + b"\r\n"
        )


def generate_zip_stream(zip_file: zipstream.ZipFile) -> Generator[bytes, None, None]:
    """Yield chunks from the ZIP file for streaming."""
    for chunk in zip_file:
        logger.debug("Yielding chunk of size: %d bytes", len(chunk))
        yield chunk


def generate_video_from_frames(
    output_file: str, frame_count: int = 100, fps: int = 30
) -> None:
    """Create a video using generated frames."""
    logger.info(
        "Generating video: %s with %d frames at %d FPS", output_file, frame_count, fps
    )

    frame_generator = generate_frames()

    # Set video properties
    frame_width, frame_height = 640, 480
    fourcc = cv2.VideoWriter_fourcc(*"mp4v")
    out = cv2.VideoWriter(output_file, fourcc, fps, (frame_width, frame_height))

    for i in range(frame_count):
        # Get the next frame
        frame = next(frame_generator)

        # Decode the JPEG back to an image
        frame_data = frame.split(b"\r\n\r\n")[1]
        img = cv2.imdecode(np.frombuffer(frame_data, dtype=np.uint8), cv2.IMREAD_COLOR)

        # Write the frame to the video
        out.write(img)
        logger.debug("Written frame %d to video", i + 1)

    out.release()
    logger.info("Video generation complete: %s", output_file)


def generate_image_response(request_type: str) -> Response:
    """Generate a random image response based on the requested type."""
    # Random image dimensions
    height = random.randint(200, 800)  # Random height
    width = random.randint(200, 800)  # Random width
    content_type = f"image/{request_type}"

    # Create a new image and draw random shapes
    img = Image.new(mode="RGB", size=(width, height), color=(255, 255, 255))
    draw = ImageDraw.Draw(img)

    # Draw a random number of shapes
    for _ in range(random.randint(5, 15)):
        shape_type = random.choice(["circle", "rectangle"])
        color = (random.randint(0, 255), random.randint(0, 255), random.randint(0, 255))

        # Define starting points for rectangle
        x1 = random.randint(0, width - 1)
        y1 = random.randint(0, height - 1)
        x2 = random.randint(x1 + 1, width)  # x2 must be greater than x1
        y2 = random.randint(y1 + 1, height)  # y2 must be greater than y1

        if shape_type == "circle":
            radius = random.randint(10, 100)
            draw.ellipse((x1, y1, x1 + radius, y1 + radius), fill=color)
        elif shape_type == "rectangle":
            draw.rectangle((x1, y1, x2, y2), fill=color)

    # Generate and draw random text on the image
    text = fake.sentence(nb_words=random.randint(2, 6))  # Generate a random sentence
    font_size = random.randint(20, 50)  # Random font size
    font_color = (
        random.randint(0, 255),
        random.randint(0, 255),
        random.randint(0, 255),
    )

    # Load a default font
    font = ImageFont.load_default()

    # Random position for the text
    text_x = random.randint(0, width - 100)  # Ensure text fits within width
    text_y = random.randint(0, height - font_size)  # Ensure text fits within height

    # Draw the text
    draw.text((text_x, text_y), text, fill=font_color, font=font)

    with BytesIO() as buf:
        img_format = "JPEG" if request_type == "jpg" else request_type.upper()
        img.save(buf, img_format)
        buf.seek(0)

        file_name = generate_random_name(f".{request_type}")

        # Log the generated image properties
        logger.info(f"Generated image of type: {img_format}, filename: {file_name}")

        response = Response(content=buf.read(), media_type=content_type)
        response.headers["Content-Disposition"] = f"attachment; filename={file_name}"

        logger.info("Image generated successfully.")

        return response


def create_random_files(num_files: int, extension: str) -> list:
    """Generate random file names and content."""
    files = []
    for _ in range(num_files):
        random_file_name = generate_random_name(extension)
        random_content = fake.paragraph().encode()
        files.append((random_file_name, random_content))
    return files


def generate_zip(file_name: str) -> BytesIO:
    """Generate ZIP file."""
    zip_buffer = BytesIO()
    with pyzipper.ZipFile(zip_buffer, "w", pyzipper.ZIP_DEFLATED) as zip_file:
        files = create_random_files(random.randint(1, 10), ".txt")
        for name, content in files:
            zip_file.writestr(name, content)
            logger.info(f"Added file to ZIP: {name}")
    zip_buffer.seek(0)
    return zip_buffer


def generate_tar(file_name: str) -> BytesIO:
    """Generate TAR file."""
    tar_buffer = BytesIO()
    with tarfile.open(fileobj=tar_buffer, mode="w") as tar_file:
        files = create_random_files(random.randint(1, 10), ".txt")
        for name, content in files:
            tar_info = tarfile.TarInfo(name=name)
            tar_info.size = len(content)
            tar_file.addfile(tar_info, BytesIO(content))
            logger.info(f"Added file to TAR: {name}")
    tar_buffer.seek(0)
    return tar_buffer


def generate_gz(file_name: str) -> BytesIO:
    """Generate GZ file."""
    gz_buffer = BytesIO()
    with pyzipper.ZipFile(gz_buffer, "w", pyzipper.ZIP_DEFLATED) as gz_file:
        files = create_random_files(random.randint(1, 10), ".txt")
        for name, content in files:
            gz_file.writestr(name, content)
            logger.info(f"Added file to GZ: {name}")
    gz_buffer.seek(0)
    return gz_buffer


def create_response(
    buffer: BytesIO, file_name: str, media_type: str
) -> StreamingResponse:
    """Helper function to create a streaming response."""
    return StreamingResponse(
        buffer,
        media_type=media_type,
        headers={"Content-Disposition": f"attachment; filename={file_name}"},
    )
