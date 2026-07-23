"""FastAPI scaffold for the RPG service.

Slice 1: health + a load probe so we can confirm the loader wiring end to end.
The turn loop, DM, and game endpoints arrive in later slices (see DESIGN.md §6)."""

from __future__ import annotations

from pathlib import Path

from dataclasses import asdict

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

from . import __version__
from .config import Settings
from .game import Game
from .llm import OllamaClient
from .loader import load_bundle_file, load_from_api
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
    return {
        "status": "ok",
        "version": __version__,
        "llm": {
            "enabled": bool(settings.ollama_model),
            "host": settings.ollama_host,
            "model": settings.ollama_model or None,
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


@app.get("/api/scenarios/{scenario_id}/summary")
def load_scenario_summary(scenario_id: int) -> dict:
    """Load a live scenario from the GHOSTS API and return a thin summary."""
    try:
        bundle = load_from_api(scenario_id, Settings.from_env())
    except Exception as exc:  # surface the upstream failure to the caller
        raise HTTPException(status_code=502, detail=f"load failed: {exc}") from exc
    return _summary(bundle)


# ── game endpoints ──────────────────────────────────────────────────────


class NewGameDto(BaseModel):
    fixture: str | None = None
    scenarioId: int | None = None


class ActDto(BaseModel):
    input: str


def _new_game_bundle(dto: NewGameDto):
    if dto.fixture:
        path = FIXTURES_DIR / f"{dto.fixture}.json"
        if not path.is_file():
            raise HTTPException(status_code=404, detail=f"fixture '{dto.fixture}' not found")
        return load_bundle_file(path)
    if dto.scenarioId is not None:
        try:
            return load_from_api(dto.scenarioId, Settings.from_env())
        except Exception as exc:
            raise HTTPException(status_code=502, detail=f"load failed: {exc}") from exc
    raise HTTPException(status_code=400, detail="provide 'fixture' or 'scenarioId'")


@app.post("/api/games")
def new_game(dto: NewGameDto) -> dict:
    """Start a game from a fixture or a live scenario; returns the first frame."""
    bundle = _new_game_bundle(dto)
    game = Game(bundle, llm=OllamaClient(Settings.from_env()))
    gid = STORE.create(game)
    frame = game.start()
    return {"gameId": gid, "frame": asdict(frame)}


@app.post("/api/games/{game_id}/act")
def act(game_id: str, dto: ActDto) -> dict:
    """Submit an option or free text; returns the resulting frame."""
    game = STORE.get(game_id)
    if game is None:
        raise HTTPException(status_code=404, detail="game not found")
    return {"gameId": game_id, "frame": asdict(game.act(dto.input))}


@app.get("/api/games/{game_id}")
def game_state(game_id: str) -> dict:
    """Current HUD without advancing the game."""
    game = STORE.get(game_id)
    if game is None:
        raise HTTPException(status_code=404, detail="game not found")
    return {"gameId": game_id, "hud": game.hud(), "isComplete": game.engine.state.is_complete}


def _summary(bundle) -> dict:
    sc = bundle.scenario
    events = sc.timeline.events if sc.timeline else []
    return {
        "id": sc.id,
        "name": sc.name,
        "events": len(events),
        "playerTurns": sum(1 for e in events if e.is_player_turn),
        "objectives": len(bundle.objectives),
        "cast": len(bundle.graph.nodes),
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
        "events": summary["events"],
        "objectives": summary["objectives"],
    }


def main() -> None:
    import uvicorn

    settings = Settings.from_env()
    uvicorn.run(app, host=settings.host, port=settings.port)
