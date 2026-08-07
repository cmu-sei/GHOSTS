"""OPFOR: offline move selection under preconditions."""

from pathlib import Path

import pytest

from ghosts_rpg.engine import Engine
from ghosts_rpg.loader import load_bundle_file
from ghosts_rpg.opfor import Opfor

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "meridian-hybrid.json"


@pytest.fixture
def setup():
    e = Engine(load_bundle_file(FIXTURE))
    e.start()
    return e, Opfor(e, llm=None)  # offline


def test_chooses_an_available_move(setup):
    engine, opfor = setup
    move = opfor.choose()
    assert move is not None
    assert move.id in {m.id for m in engine.available_moves()}


def test_offline_picks_highest_progress(setup):
    engine, opfor = setup
    # At T+0, seed-narrative and harvest-creds (progress 1) are available; the
    # greedy stub takes the highest-progress, tie-broken by id.
    move = opfor.choose()
    available = engine.available_moves()
    assert move.progress == max(m.progress for m in available)


def test_returns_none_when_no_move_available(setup):
    engine, opfor = setup
    # Spend every move by marking them played.
    engine.state.opfor_moves_played = [m.id for m in engine.scenario.opfor.playbook]
    assert opfor.choose() is None
