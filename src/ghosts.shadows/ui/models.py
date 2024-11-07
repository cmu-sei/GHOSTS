# models.py
from pydantic import BaseModel


class Token(BaseModel):
    """Model for representing an access token."""

    access_token: str  # The JWT access token string
    token_type: str  # The type of token, typically "bearer"


class UserSignupRequest(BaseModel):
    """Model for user signup request data."""

    username: str
    password: str


class UserLoginRequest(BaseModel):
    """Model for user login request data."""

    username: str
    password: str
