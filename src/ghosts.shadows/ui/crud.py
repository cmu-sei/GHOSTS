from sqlalchemy.orm import Session
from models import UserSignupRequest
from security import get_password_hash
from database import User
from typing import Optional


def get_user(db: Session, username: str) -> Optional[User]:
    """
    Retrieve a user by username from the database.

    Args:
        db (Session): The database session.
        username (str): The username of the user to retrieve.

    Returns:
        Optional[User]: The User object if found, otherwise None.
    """
    return db.query(User).filter(User.username == username).first()


def create_user(db: Session, user_request: UserSignupRequest) -> User:
    """
    Create a new user in the database.

    Args:
        db (Session): The database session.
        user_request (UserSignupRequest): The request data containing username and password.

    Returns:
        User: The newly created User object.

    Raises:
        Exception: If a user with the same username already exists.
    """
    existing_user = get_user(db, user_request.username)
    if existing_user:
        raise Exception("User already exists with this username.")

    db_user = User(
        username=user_request.username,
        hashed_password=get_password_hash(user_request.password),
    )

    db.add(db_user)
    db.commit()
    db.refresh(db_user)

    return db_user
