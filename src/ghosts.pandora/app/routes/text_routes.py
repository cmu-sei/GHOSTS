from fastapi import APIRouter, Response
import app_logging
import random
from faker import Faker
from utils.helper import generate_random_name
from utils.ollama import generate_document_with_ollama
from config.config import OLLAMA_ENABLED

router = APIRouter()
logger = app_logging.setup_logger("app_logger")

# Initialize Faker for fallback content generation
fake = Faker()

model = "llama3.2"


@router.get("/text")
@router.post("/text")
@router.get("/text/{filename:path}", response_class=Response, tags=["Documents"])
@router.post("/text/{filename:path}", tags=["Documents"])
def return_text(
    filename: str = None,
) -> Response:
    """Generate a document with content from Ollama if enabled; otherwise, fallback to Faker."""

    # Determine the effective filename
    effective_filename = filename.split(".")[0] if filename else None
    logger.info(
        "Text generation request received with filename: %s", effective_filename
    )

    try:
        if OLLAMA_ENABLED:
            # Prepare the prompt based on the effective filename
            prompt = (
                f"Create a document titled '{effective_filename}' with several paragraphs on the topic given in the title. Only return the contents of the document and not any other content"
                if effective_filename
                else "Create a document with several paragraphs about a chosen topic."
            )
            logger.info("Sending request to Ollama model with prompt: %s", prompt)

            generated_content = generate_document_with_ollama(prompt, model)

            if generated_content:
                # Create the response with plain text content
                response = Response(
                    content=generated_content.encode("utf8"), media_type="text/plain"
                )

                # Set the filename to either effective_filename or a generated random name if None
                response.headers["Content-Disposition"] = (
                    f"inline; filename={effective_filename + '.txt' if effective_filename else generate_random_name('.txt')}"
                )
                logger.info("Text document generated successfully using Ollama.")
                return response
            else:
                logger.info("Falling back to Faker for content generation.")

        # Fallback to Faker for content generation
        logger.info("Falling back to Faker for content generation.")

        # Generate a random heading
        heading = fake.sentence(nb_words=random.randint(3, 6)).strip(".")
        logger.info(f"Generated heading: {heading}")

        # Randomize the number of subheadings (1 to 3)
        num_subheadings = random.randint(1, 3)
        logger.info(f"Number of subheadings to generate: {num_subheadings}")

        subheadings = [
            fake.sentence(nb_words=random.randint(3, 6)).strip(".")
            for _ in range(num_subheadings)
        ]
        logger.info(f"Generated subheadings: {subheadings}")

        # Generate a paragraph for each subheading
        paragraphs = []
        for i in range(num_subheadings):
            num_sentences = random.randint(20, 150)
            paragraph = fake.paragraph(nb_sentences=num_sentences)
            paragraphs.append(paragraph)
            logger.info(
                f"Generated paragraph with {num_sentences} sentences for subheading '{subheadings[i]}'."
            )

        # Create the HTML document
        document_lines = [f"<h1>{heading}</h1>"]
        for subheading, paragraph in zip(subheadings, paragraphs):
            document_lines.append(f"<h2>{subheading}</h2>")
            document_lines.append(f"<p>{paragraph}</p>")
            logger.info(f"Added subheading and paragraph to document: {subheading}")

        # Join the document lines into a single string
        document = "\n".join(document_lines)

        # Create the response with HTML content
        response = Response(content=document.encode("utf8"), media_type="text/html")

        # Set the filename to either effective_filename or 'document' if None
        response.headers["Content-Disposition"] = (
            f"inline; filename={effective_filename + '.html' if effective_filename else 'document.html'}"
        )

        logger.info("HTML document generated successfully using Faker.")
        return response

    except Exception as e:
        logger.error(f"Unexpected error while generating document: {str(e)}")
        return Response(
            content="An unexpected error occurred while generating the document.",
            status_code=500,
        )
