"""Utility for computing reproducible hashes of prompt templates."""

import hashlib
from pathlib import Path

_PROMPTS_DIR = Path(__file__).parent / "prompts"


def compute_prompt_hash() -> str:
    """SHA-256 hash of the combined prompt template contents."""
    hasher = hashlib.sha256()
    for name in sorted(["leader_system.jinja2", "leader_event.jinja2"]):
        path = _PROMPTS_DIR / name
        if path.exists():
            hasher.update(path.read_bytes())
    return hasher.hexdigest()[:16]
