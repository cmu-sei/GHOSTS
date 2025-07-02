import os
import mimetypes
from urllib.parse import unquote
from fastapi import Request
from app_logging import setup_logger
from config.config import STORE_RESULTS
logger = setup_logger(__name__)

class ContentManager:
    def __init__(self, base_dir="_data", default="index", extension="txt"):
        self.base_dir = base_dir
        self.default = default
        self.extension = extension
        self.file_name = None
        self.rel_path = None
        self.full_path = None
        self._store = STORE_RESULTS

    def is_storing(self) -> bool:
        return self._store

    def resolve(self, request: Request):
        self.request = request
        url_path = unquote(request.url.path).lstrip("/")
        
        if url_path.endswith("/"):
            url_path += f"{self.default}.{self.extension}"
        elif not url_path.endswith(f".{self.extension}"):
            url_path += f".{self.extension}"

        self.rel_path = url_path
        self.file_name = os.path.basename(url_path)
        self.full_path = os.path.join(self.base_dir, url_path)
        
        os.makedirs(os.path.dirname(self.full_path), exist_ok=True)

        logger.info(f"{self.extension.upper()} request received. Filename: {self.file_name} Path: {self.rel_path}")

    def guess_media_type(self) -> str:
        media_type, _ = mimetypes.guess_type(self.full_path)
        return media_type or "application/octet-stream"

    def load(self) -> str | None:
        try:
            if not self.full_path or not os.path.exists(self.full_path):
                return None
            with open(self.full_path, "r", encoding="utf-8") as f:
                return f.read()
        except:
            return None

    def save(self, content: str | bytes):
        try:
            os.makedirs(os.path.dirname(self.full_path), exist_ok=True)
            mode = "wb" if isinstance(content, bytes) else "w"
            with open(self.full_path, mode) as f:
                f.write(content)
        except:
            return None
