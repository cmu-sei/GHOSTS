from sqlalchemy import create_engine, Column, Integer, String
from sqlalchemy.ext.declarative import declarative_base
from sqlalchemy.orm import sessionmaker
from sqlalchemy.engine import Engine
from sqlalchemy.orm import Session

# Database configuration
DATABASE_URL = "sqlite:///./test.db"

# Database engine and session management
engine: Engine = create_engine(DATABASE_URL, connect_args={"check_same_thread": False})
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)
Base = declarative_base()


class User(Base):
    """
    User model representing the users table in the database.
    """

    __tablename__ = "users"

    id: int = Column(Integer, primary_key=True, index=True)
    username: str = Column(String, unique=True, index=True)
    hashed_password: str = Column(String)


def init_db() -> None:
    """
    Initialize the database by creating the necessary tables.

    This function creates all tables defined by the SQLAlchemy models
    that inherit from the Base class. It should be called once to set
    up the database schema.
    """
    Base.metadata.create_all(bind=engine)


def get_db() -> Session:
    """Dependency that provides a database session."""
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
