# GHOSTS SHADOWS API

## Overview

GHOSTS SHADOWS is a FastAPI-based web server designed to facilitate interactions within a dynamic environment. The API provides various endpoints for activities, chat, content, lessons, and social interactions, enabling users to access and manipulate data in real-time.

## Features

- **RESTful API**: Comprehensive set of routes for different functionalities.
- **CORS Support**: Configured to allow requests from specified origins.
- **Static File Serving**: Ability to serve static files from the `assets` directory.
- **Detailed Logging**: Extensive logging for monitoring and debugging purposes.
- **Exception Handling**: Middleware for graceful error handling and logging of exceptions.
- **Interactive API Documentation**: Automatically generated Swagger UI available at `/docs`.

## Getting Started

### Prerequisites

- Python 3.10 or higher
- FastAPI
- Uvicorn
- Additional dependencies specified in the `requirements.txt`

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/cmu-sei/GHOSTS
   cd src/ghosts-shadows/api
   ```

2. Create a virtual environment:
   ```bash
   python -m venv venv
   source venv/bin/activate  # On Windows use `venv\Scripts\activate`
   ```

3. Install the required dependencies:
   ```bash
   pip install -r requirements.txt
   ```

### Configuration

Before running the application, you may need to configure the environment variables. You can set `GHOSTS_OLLAMA_URL` to specify the Ollama host.

Example:
```bash
export GHOSTS_OLLAMA_URL=http://localhost:11434
```

### Running the Application

To start the FastAPI server, run the following command:

```bash
python main.py
```

### API Endpoints

- **Root Endpoint**: `GET /`
  - Returns a welcome message.
  
- **Interactive API Documentation**: Access the Swagger UI at `/docs` to explore available API endpoints and test them interactively.

#### Available Endpoints

1. **POST /activity**
   - Processes an activity request from the user.

2. **POST /chat**
   - Processes a chat request from the user.

3. **POST /content**
   - Processes a content request from the user.

4. **POST /lessons**
   - Processes a lessons request from the user.

5. **POST /social**
   - Processes a social interaction request from the user.

### Static Files

Static files can be served from the `/assets` directory. Ensure that you place your static files in this folder for them to be accessible.

## Logging

The application uses a logging setup configured through the `app_logging` module. Logging levels can be adjusted as needed. By default, the logging level is set to DEBUG for detailed output.

## Error Handling

The application includes middleware for logging exceptions. Errors are caught and logged, and appropriate HTTP status codes are returned to the client. This ensures that both HTTP exceptions and unhandled exceptions are properly logged for diagnostics.

## Middleware

- **CORS Middleware**: Configured to allow requests from specified origins, facilitating cross-origin requests from specified clients (e.g., `http://localhost:7860`).
- **Exception Logging Middleware**: Logs exceptions that occur during request processing to assist with debugging and monitoring.

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests to improve the project.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
