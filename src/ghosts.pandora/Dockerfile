# Use the slim variant of Python 3.10
FROM python:3.10-slim

# Set the working directory in the container
WORKDIR /app

# Install required system packages and Python dependencies
RUN apt-get update && \
    apt-get install --no-install-recommends -y gcc libffi-dev libgl1-mesa-glx libglib2.0-0 ffmpeg espeak-ng && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy only the requirements file first
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy the rest of the application code
COPY app ./

# Set environment variables (proxy can be omitted if not needed)
ENV HTTP_PROXY=""
ENV HTTPS_PROXY=""
ENV OLLAMA_API_URL="http://ollama:11434/"

# Expose port 80
EXPOSE 80

# Command to run the FastAPI app with Uvicorn
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "80"]
