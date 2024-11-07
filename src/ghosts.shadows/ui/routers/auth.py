# routers/auth.py
from fastapi import APIRouter, HTTPException, status
from database import SessionLocal
from models import UserSignupRequest, UserLoginRequest
from crud import create_user, get_user
from security import create_access_token, verify_password

router = APIRouter()


@router.post("/signup")
async def signup(request: UserSignupRequest) -> dict:
    """Register a new user with a hashed password."""
    db = SessionLocal()
    try:
        if get_user(db, request.username):
            raise HTTPException(status_code=400, detail="Username already registered")
        create_user(db, request)
    finally:
        db.close()
    return {"message": "User created successfully"}


@router.post("/token")
async def login_for_access_token(login_request: UserLoginRequest) -> dict:
    """Authenticate user and return a JWT token."""
    db = SessionLocal()
    try:
        user = get_user(db, login_request.username)
        if not user or not verify_password(
            login_request.password, user.hashed_password
        ):
            raise HTTPException(
                status_code=status.HTTP_401_UNAUTHORIZED,
                detail="Incorrect username or password",
            )
        access_token = create_access_token(data={"sub": login_request.username})
        return {"access_token": access_token, "token_type": "bearer"}
    finally:
        db.close()
