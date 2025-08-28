FROM python:3.13-slim

WORKDIR /app

RUN apt-get update && \
    apt-get install --no-install-recommends -y \
    gcc \
    libffi-dev \
    libgl1 \
    libglib2.0-0 \
    ffmpeg \
    espeak-ng \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./

ENV OLLAMA_API_URL="http://ollama:11434/"
EXPOSE 80

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "80"]
