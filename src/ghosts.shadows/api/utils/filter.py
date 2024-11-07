import re


def filter(content):
    # Split content into sentences by punctuation
    content_sentences = re.split(r"([:|.!?])", content)
    content_sentences = [
        (content_sentences[i] + content_sentences[i + 1])
        for i in range(0, len(content_sentences) - 1, 2)
    ]

    for i, sentence in enumerate(content_sentences):
        # Remove text before a colon
        if re.match(r"^[^:]*:", sentence):
            sentence = re.sub(r"^[^:]*:", "", sentence)

        # Remove text before comma, capitalize remaining text
        if re.match(r"\b\w+,\s*", sentence):
            sentence = re.sub(r"\b\w+,\s*", "", sentence)
            if sentence:
                sentence = sentence[0].upper() + sentence[1:]

        # Remove specific patterns followed by exclamation marks
        sentence = re.sub(r"^\w+!", "", sentence)
        sentence = re.sub(r"^\w+\s\w+!", "", sentence)

        # Remove "here" phrases like "here is", "here are"
        sentence = re.sub(
            r"^\s*.*\b(here is|here are|here's a|here's an|here's the|here's)\b.*$",
            "",
            sentence,
            flags=re.IGNORECASE,
        )

        # Remove unwanted phrases such as "AI-powered", "like to know"
        sentence = re.sub(
            r"^\s*.*\b(excel|URL|a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you|to optimize this tweet|provide one or two relevant hashtags for the tweet)\b.*$",
            "",
            sentence,
            flags=re.IGNORECASE,
        )

        # Remove phrases like "for demonstration purposes", "random", "based on the context"
        sentence = re.sub(
            r"^\s*.*\b(demonstration purposes|random|based on the context|randomly|just examples|hypothetical|may contain|based on the information|based on the provided context)\b.*$",
            "",
            sentence,
            flags=re.IGNORECASE,
        )

        # Remove any email addresses
        sentence = re.sub(
            r"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "", sentence
        )

        # Remove emojis
        emoji_pattern = re.compile(
            "["
            "\U0001f600-\U0001f64f"  # emoticons
            "\U0001f300-\U0001f5ff"  # symbols & pictographs
            "\U0001f680-\U0001f6ff"  # transport & map symbols
            "\U0001f1e0-\U0001f1ff"  # flags (iOS)
            "]+",
            flags=re.UNICODE,
        )
        sentence = emoji_pattern.sub(r"", sentence)  # Remove emojis

        content_sentences[i] = sentence.strip()  # Update sentence

    # Reconstruct the content from filtered sentences
    content = " ".join(content_sentences)
    return content.strip()
