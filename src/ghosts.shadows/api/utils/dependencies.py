from jose import jwt, JWTError
from fastapi import Header, HTTPException
from config.config import SECRET_KEY, ALGORITHM
import logging
from pydantic import BaseModel

logger = logging.getLogger(__name__)


async def decode_jwt(authorization: str = Header(None)):
    logger.debug(f"Authorization header received: {authorization}")  # Log the header
    if authorization and authorization.startswith("Bearer "):
        token = authorization.split("Bearer ")[1]
        try:
            payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
            username: str = payload.get("sub")
            if not username:
                raise ValueError("Invalid token payload")
            return username
        except JWTError as e:
            logger.error(f"JWT Error: {str(e)}")  # Log the exception message
            raise HTTPException(status_code=401, detail="Invalid token")
    logger.error("No token provided")  # Log if no token is provided
    raise HTTPException(status_code=401, detail="No token provided")


class Request(BaseModel):
    query: str
