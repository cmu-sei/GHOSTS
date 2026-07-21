"""In-memory session store for running games.

One process, one store. Good enough for single-player local play; swap for a
persistent store later if multi-seat arrives (DESIGN.md defers that)."""

from __future__ import annotations

import secrets

from .game import Game


class SessionStore:
    def __init__(self) -> None:
        self._games: dict[str, Game] = {}

    def create(self, game: Game) -> str:
        gid = secrets.token_hex(8)
        self._games[gid] = game
        return gid

    def get(self, gid: str) -> Game | None:
        return self._games.get(gid)

    def drop(self, gid: str) -> None:
        self._games.pop(gid, None)


STORE = SessionStore()
