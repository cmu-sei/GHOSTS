from fastapi import APIRouter, Response
from io import BytesIO
from pptx import Presentation
from faker import Faker
import app_logging
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama
from config.config import OLLAMA_ENABLED

router = APIRouter()
fake = Faker()
logger = app_logging.setup_logger("app_logger")

model = "llama3.2"  # Specify the model you want to use with Ollama


def split_content_to_bullets(content: str) -> str:
    """Split content into bullet points if it exceeds a certain length."""
    max_length = 200  # Set a maximum length for content per bullet
    bullets = content.split(". ")  # Split by sentences
    formatted_content = []

    for bullet in bullets:
        if len(bullet) > max_length:
            # Truncate if necessary and add an ellipsis
            bullet = bullet[:max_length].rstrip() + "..."
        formatted_content.append(f"• {bullet.strip()}")

    return "\n".join(formatted_content)


@router.get("/slides", response_class=Response, tags=["Presentations"])
@router.post("/slides", tags=["Presentations"])
@router.get("/ppt", tags=["Presentations"])
@router.post("/ppt", tags=["Presentations"])
@router.get("/ppt/{file_name}", tags=["Presentations"])
@router.post("/ppt/{file_name}", tags=["Presentations"])
def return_ppt(file_name: str = None) -> Response:
    """Return a PowerPoint presentation with random content."""
    logger.info("PowerPoint generation request received.")

    if file_name is None:
        file_name = generate_random_name(".pptx")  # Use random name generation
        logger.info("Generated random file name: %s", file_name)

    try:
        presentation = Presentation()

        # Prepare prompts for slide content
        title_prompt = "Provide just the title for a PowerPoint slide."
        content_prompt = f"Provide just the content for a PowerPoint presentation slide with the title {title_prompt}"

        title = fake.sentence()  # Default to Faker title
        content = fake.paragraph(nb_sentences=3)  # Default to Faker content

        if OLLAMA_ENABLED:
            logger.info("Generating title using Ollama.")
            try:
                title = generate_document_with_ollama(title_prompt, model) or title
                logger.info("Generated title: %s", title)
            except Exception as e:
                logger.warning(
                    "Ollama call for title failed: %s. Falling back to Faker.", str(e)
                )

            logger.info("Generating content using Ollama.")
            try:
                content = (
                    generate_document_with_ollama(content_prompt, model) or content
                )
                logger.info("Generated content: %s", content)
            except Exception as e:
                logger.warning(
                    "Ollama call for content failed: %s. Falling back to Faker.", str(e)
                )

        # Split and format the content into bullet points
        formatted_content = split_content_to_bullets(content)

        # Add a title slide
        slide_layout = presentation.slide_layouts[0]  # 0 is the layout for title slide
        slide = presentation.slides.add_slide(slide_layout)
        title_shape = slide.shapes.title
        subtitle_shape = slide.placeholders[1]
        title_shape.text = title
        subtitle_shape.text = fake.sentence()  # Keep subtitle from Faker for variety
        logger.debug(
            "Added title slide with title: %s and subtitle: %s",
            title_shape.text,
            subtitle_shape.text,
        )

        # Add a content slide
        slide_layout = presentation.slide_layouts[
            1
        ]  # 1 is the layout for content slide
        slide = presentation.slides.add_slide(slide_layout)
        title_shape = slide.shapes.title
        content_shape = slide.placeholders[1]
        title_shape.text = title  # Reusing title for content slide for demonstration
        content_shape.text = formatted_content  # Set formatted content as text
        logger.debug("Added content slide with title: %s", title_shape.text)

        # Save the presentation to a BytesIO buffer
        buf = BytesIO()
        presentation.save(buf)
        buf.seek(0)  # Reset the buffer's position to the beginning

        response = Response(
            content=buf.getvalue(),
            media_type="application/vnd.openxmlformats-officedocument.presentationml.presentation",
        )
        response.headers["Content-Disposition"] = f"attachment; filename={file_name}"

        logger.info("PowerPoint file generated successfully: %s", file_name)
        return response

    except Exception as e:
        logger.error("Error while generating PowerPoint: %s", str(e))
        return Response(
            content="An error occurred while generating the PowerPoint presentation.",
            status_code=500,
        )
