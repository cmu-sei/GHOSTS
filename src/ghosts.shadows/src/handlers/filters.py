import re


def filter_llm_response(content):
    content_sentences = re.split("([\:|\.|!|\?])", content)
    content_sentences = [
        (content_sentences[i] + content_sentences[i + 1])
        for i in range(0, len(content_sentences) // 2, 2)
    ]
    for i, sentence in enumerate(content_sentences):
        if re.match(r"\b\w+,\s*", sentence):
            sentence = re.sub(r"\b\w+,\s*", "", sentence)
            if sentence:
                sentence = sentence[0].upper() + sentence[1:]

        if re.match(r"^\s*\w+!", sentence):
            sentence = re.sub(r"^\w+!", "", sentence)

        if re.match(r"^\s*\w+\s\w+!", sentence):
            sentence = re.sub(r"^\w+\s\w+!", "", sentence)

        if re.match(
            r"^\s*.*\b(here is|here are|here's a|here's an|here's the|here's)\b.*$",
            sentence,
            flags=re.IGNORECASE,
        ):
            sentence = re.sub(
                r"^\s*.*\b(here is|here are|here's a|here's an|here's the|here's)\b.*$",
                "",
                sentence,
                flags=re.IGNORECASE,
            )

        if re.match(
            r"^\s*.*\b(a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you|)\b.*$",
            sentence,
            flags=re.IGNORECASE,
        ):
            sentence = re.sub(
                r"^\s*.*\b(a sentence about|a paragraph about|like to know|I hope|AI-powered|be happy to help|can help you)\b.*$",
                "",
                sentence,
                flags=re.IGNORECASE,
            )

        if re.match(
            r"^\s*.*\b(hypothetical|may contain|based on the context provided|based on the provided context)\b.*$",
            sentence,
            flags=re.IGNORECASE,
        ):
            sentence = re.sub(
                r"^\s*.*\b(hypothetical|may contain|based on the context provided|based on the provided context)\b.*$",
                "",
                sentence,
                flags=re.IGNORECASE,
            )

        emoji_pattern = re.compile(
            "["
            "\U0001f600-\U0001f64f"  # emoticons
            "\U0001f300-\U0001f5ff"  # symbols & pictographs
            "\U0001f680-\U0001f6ff"  # transport & map symbols
            "\U0001f1e0-\U0001f1ff"  # flags (iOS)
            "]+",
            flags=re.UNICODE,
        )
        sentence = emoji_pattern.sub(r"", sentence)  # no emoji
        content_sentences[i] = sentence

    content = " ".join(content_sentences)
    return content.strip()
