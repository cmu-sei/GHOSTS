"""FastAPI scaffold for the RPG service — Kriegspiel Mode.

Serves the scenario catalog and the game turn loop: start a game from a fixture,
then submit action+rationale turns and get back frames the Angular UI renders."""

from __future__ import annotations

from pathlib import Path

from dataclasses import asdict

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from . import __version__
from .config import Settings
from .game import Game
from .llm import make_llm
from .loader import load_bundle_file
from .session import STORE

FIXTURES_DIR = Path(__file__).resolve().parents[2] / "fixtures" / "scenarios"

app = FastAPI(title="GHOSTS RPG — Scenario Player", version=__version__)

# Dev CORS: the Angular UI (port 4300) talks to this service directly.
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
def health() -> dict:
    settings = Settings.from_env()
    llm = make_llm(settings)
    if settings.llm_provider == "bedrock":
        endpoint, model = f"bedrock:{settings.bedrock_region}", settings.bedrock_model
    else:
        endpoint, model = settings.ollama_host, settings.ollama_model
    return {
        "status": "ok",
        "version": __version__,
        "llm": {
            "provider": settings.llm_provider,
            "enabled": llm.enabled,
            "host": endpoint,
            "model": model or None,
        },
    }


@app.get("/api/fixtures")
def list_fixtures() -> dict:
    """List player-facing bundled scenarios available to load offline."""
    if not FIXTURES_DIR.is_dir():
        return {"fixtures": []}
    fixtures = []
    for path in FIXTURES_DIR.glob("*.json"):
        bundle = load_bundle_file(path)
        if bundle.catalog is None or not bundle.catalog.listed:
            continue
        fixtures.append(_catalog_entry(path.stem, bundle))
    fixtures.sort(key=lambda item: (item["sortOrder"], item["name"]))
    return {"fixtures": fixtures}


@app.get("/api/fixtures/{name}")
def load_fixture(name: str) -> dict:
    """Load a fixture bundle and return a thin summary (load-path smoke probe)."""
    path = FIXTURES_DIR / f"{name}.json"
    if not path.is_file():
        raise HTTPException(status_code=404, detail=f"fixture '{name}' not found")
    bundle = load_bundle_file(path)
    return _summary(bundle)


# ── game endpoints ──────────────────────────────────────────────────────


class NewGameDto(BaseModel):
    fixture: str | None = None


class ActDto(BaseModel):
    action: str
    rationale: str = ""


def _new_game_bundle(dto: NewGameDto):
    if not dto.fixture:
        raise HTTPException(status_code=400, detail="provide 'fixture'")
    path = FIXTURES_DIR / f"{dto.fixture}.json"
    if not path.is_file():
        raise HTTPException(status_code=404, detail=f"fixture '{dto.fixture}' not found")
    return load_bundle_file(path)


@app.post("/api/games")
def new_game(dto: NewGameDto) -> dict:
    """Start a game from a fixture; returns the first frame."""
    bundle = _new_game_bundle(dto)
    game = Game(bundle, llm=make_llm(Settings.from_env()))
    gid = STORE.create(game)
    frame = game.start()
    return {"gameId": gid, "frame": asdict(frame)}


@app.post("/api/games/{game_id}/act")
def act(game_id: str, dto: ActDto) -> dict:
    """Submit an action + rationale; returns the resulting frame."""
    game = STORE.get(game_id)
    if game is None:
        raise HTTPException(status_code=404, detail="game not found")
    return {"gameId": game_id, "frame": asdict(game.act(dto.action, dto.rationale))}


@app.get("/api/games/{game_id}")
def game_state(game_id: str) -> dict:
    """Current HUD without advancing the game."""
    game = STORE.get(game_id)
    if game is None:
        raise HTTPException(status_code=404, detail="game not found")
    return {"gameId": game_id, "hud": game.hud(), "isComplete": game.engine.state.is_complete}


def _summary(bundle) -> dict:
    sc = bundle.scenario
    return {
        "id": sc.id,
        "name": sc.name,
        "objectives": len(sc.objectives),
        "opforMoves": len(sc.opfor.playbook),
        "triggers": len(sc.triggers),
    }


def _catalog_entry(fixture: str, bundle) -> dict:
    summary = _summary(bundle)
    catalog = bundle.catalog
    return {
        "fixture": fixture,
        "sortOrder": catalog.sort_order,
        "name": bundle.scenario.name,
        "description": bundle.scenario.description,
        "era": catalog.era,
        "theater": catalog.theater,
        "estimatedMinutes": catalog.estimated_minutes,
        "objectives": summary["objectives"],
    }


def main() -> None:
    import uvicorn

    settings = Settings.from_env()
    uvicorn.run(app, host=settings.host, port=settings.port)
