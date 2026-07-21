"""Full-game orchestration: both branches play through to an AAR (offline DM)."""

from pathlib import Path

import pytest

from ghosts_rpg.game import Game
from ghosts_rpg.loader import load_bundle_file

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "phishing-drill.json"


@pytest.fixture
def game():
    return Game(load_bundle_file(FIXTURE))  # llm=None -> offline DM


def test_start_stops_at_first_player_turn(game):
    frame = game.start()
    assert frame.awaiting_player
    # Beats narrated steps 1-4 (White, Red, White, Blue).
    assert [b.step_number for b in frame.beats] == [1, 2, 3, 4]
    assert frame.beats[-1].cell == "Blue Team"
    assert frame.tasks
    assert frame.tasks[0]["actions"]


def test_contain_path_wins(game):
    game.start()
    game.act("investigate")  # recon, stays on step 4
    frame = game.act("quarantine the email and block the domain")  # decisive
    # Engine took the contained branch (step 5), then to player step 7.
    assert any(b.step_number == 5 for b in frame.beats)
    assert not any(b.step_number == 6 for b in frame.beats)
    frame = game.act("stand down")
    assert frame.is_complete
    assert frame.aar["outcome"] == "WIN"
    assert "contained" in game.engine.state.flags


def test_wait_path_loses(game):
    game.start()
    frame = game.act("wait and watch")  # no containment
    # Engine took the breach branch (step 6), skipping 5.
    assert any(b.step_number == 6 for b in frame.beats)
    assert not any(b.step_number == 5 for b in frame.beats)
    frame = game.act("isolate the host")
    assert frame.is_complete
    assert frame.aar["outcome"] == "LOSS"


def test_unrecognized_input_is_no_effect(game):
    game.start()
    frame = game.act("xyzzy")
    assert frame.awaiting_player  # still our turn
    assert any("No effect" in n for n in frame.notices)


def test_investigate_meets_detect_objective(game):
    game.start()
    game.act("investigate the lookalike domain")
    assert game.engine.state.objective_status[1] == "Achieved"


def test_hud_reports_state(game):
    frame = game.start()
    assert frame.hud["role"].startswith("Blue Team")
    assert frame.hud["step"] == 4
    assert "Crimson Tide" in frame.hud["threats"]
    assert len(frame.hud["objectives"]) == 2
