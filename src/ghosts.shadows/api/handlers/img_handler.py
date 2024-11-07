from imaginairy.api import imagine
from imaginairy.schema import ImaginePrompt


def main(prompt):
    prompts = [
        ImaginePrompt(prompt),
    ]
    for result in imagine(prompts):
        result.save("my_image.jpg")
    return
