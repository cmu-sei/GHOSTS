from fastapi import FastAPI, Request
from fastapi.staticfiles import StaticFiles
from fastapi.templating import Jinja2Templates
from database import init_db
from routers import auth, users
import app_logging
from typing import Any

# Set up the logger
logger = app_logging.setup_logger(__name__)

# FastAPI application instance
app = FastAPI()

# Jinja2 templates for rendering HTML
app.mount("/static", StaticFiles(directory="static"), name="static")
templates = Jinja2Templates(directory="templates")

# Initialize the database
init_db()

# Include routers
app.include_router(auth.router)
app.include_router(users.router)


@app.get("/")
async def get_spa(request: Request) -> Any:
    """Serve the main HTML file for the single-page application."""
    return templates.TemplateResponse("index.html", {"request": request})


# This section ensures that Uvicorn runs correctly
if __name__ == "__main__":
    import uvicorn

    uvicorn.run("main:app", host="0.0.0.0", port=7860, reload=True)
