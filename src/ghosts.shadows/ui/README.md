# Ghost Shadows API User Interface

This project provides a web-based user interface for interacting with the authentication and API services of the Ghosts Shadows application. It allows users to sign up, log in, and make queries to the Shadows API through a responsive FastAPI application and HTML-based interface.

## Features

- **User Authentication**: Users can sign up and log in to obtain access tokens.
- **API Querying**: After logging in, users can query the Shadows API, selecting from various models and sending requests directly from the interface.
- **Responsive Interface**: Built with HTML, CSS, and JavaScript, offering a simple UI with separate sections for sign-up, login, and API querying.

## Project Structure

```
app/
│
├── main.py               # Main entry point of the application
├── database.py           # Database connection and models
├── crud.py               # CRUD operations
├── models.py             # Pydantic models
├── security.py           # Security functions (hashing, token creation)
├── app_logging.py        # Logging configuration and setup
├── config.py             # Configuration settings (e.g., URLs and secret keys)
├── routers/              # API routers
│   ├── auth.py           # Authentication routes
│   └── users.py          # User-related routes
├── static/               # Static files (JS, CSS)
└── templates/            # Jinja2 templates
```

## Requirements

- Python 3.10+
- FastAPI
- `jose` for JWT token handling
- `passlib` for password hashing
- Requests

Install the required packages using pip:

```bash
pip install -r requirements
```

## Environment Variables

The application uses the following environment variables to configure the authentication and Shadows service URLs:

- `AUTH_URL`: Base URL for the authentication service (default: `http://0.0.0.0:7860`)
- `SHADOWS_URL`: Base URL for the Shadows API service (default: `http://0.0.0.0:5900`)

Set these variables in your terminal before running the application:

```bash
export AUTH_URL="http://your_auth_service_url"
export SHADOWS_URL="http://your_shadows_api_url"
```

## Running the Application

To start the FastAPI application, use either:

```bash
uvicorn app.main:app --host 0.0.0.0 --port 7860
```

or 

```bash
python app/main.py
```

The application will be accessible at `http://0.0.0.0:7860`.

## Usage

1. **Sign Up**:
   - Go to the main page and enter a username and password in the "Signup" section.
   - Click "Signup" to register.

2. **Login**:
   - Enter your credentials in the "Login" section.
   - Click "Login" to receive an access token, which is stored locally in the browser for authenticated requests.

3. **Query the API**:
   - In the "Query Model" section, choose a model from the dropdown, enter your query, and click "Get User Info" to submit it to the Shadows API.
   - The API response will be displayed in the "API Response" area.

## Troubleshooting

- Ensure the authentication and Shadows services are running and accessible on the configured URLs.
- Check the FastAPI logs for error messages if the application is not behaving as expected.

## Contributing

If you would like to contribute to this project, please feel free to submit a pull request or open an issue.

## License

This project is licensed under the MIT License. See the LICENSE file for details.
