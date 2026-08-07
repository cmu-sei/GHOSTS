"""Engine: condition grammar, delta validation, move/trigger gating, roll, end."""

from pathlib import Path

import pytest

from ghosts_rpg.engine import Deltas, Engine
from ghosts_rpg.loader import load_bundle_file
from ghosts_rpg.models import OddsBand, OutcomeTier

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "meridian-hybrid.json"


@pytest.fixture
def engine():
    e = Engine(load_bundle_file(FIXTURE))
    e.start()
    return e


def test_condition_grammar(engine):
    assert engine.evaluate_condition(None) is True
    assert engine.evaluate_condition("") is True
    assert engine.evaluate_condition("!has-foothold") is True  # flag not set
    assert engine.evaluate_condition("flag:has-foothold") is False
    engine.state.flags.add("has-foothold")
    assert engine.evaluate_condition("flag:has-foothold") is True
    assert engine.evaluate_condition("!has-foothold") is False


def test_condition_grammar_clock_and_conjunction(engine):
    assert engine.evaluate_condition("clock>=40") is False
    engine.state.clock_minutes = 45
    assert engine.evaluate_condition("clock>=40") is True
    engine.state.flags.add("near-fileshare")
    assert engine.evaluate_condition("flag:near-fileshare && clock>=40") is True
    assert engine.evaluate_condition("flag:near-fileshare && !host-isolated") is True


def test_unparseable_term_gates(engine):
    assert engine.evaluate_condition("wat:nonsense") is False


def test_available_moves_respect_preconds_and_spent(engine):
    ids = {m.id for m in engine.available_moves()}
    # Only moves with satisfied preconds are available at T+0.
    assert "harvest-creds" in ids  # precond !creds-reset
    assert "seed-narrative" in ids  # precond !narrative-countered
    assert "spread-lateral" not in ids  # needs flag:has-foothold
    # Playing harvest-creds sets has-foothold, opening spread-lateral.
    move = next(m for m in engine.available_moves() if m.id == "harvest-creds")
    engine.apply_move(move)
    ids2 = {m.id for m in engine.available_moves()}
    assert "harvest-creds" not in ids2  # already played
    assert "spread-lateral" in ids2


def test_pending_triggers_fire_once(engine):
    assert engine.pending_triggers() == []
    engine.state.clock_minutes = 40
    pending = engine.pending_triggers()
    assert any(t.id == "press-deadline" for t in pending)
    engine.fire_trigger(pending[0])
    assert all(t.id != pending[0].id for t in engine.pending_triggers())


def test_roll_is_seeded_and_deterministic(engine):
    engine.state.turn = 3
    first = engine.roll(OddsBand.EVEN)
    second = engine.roll(OddsBand.EVEN)
    assert first == second  # same turn+band -> same tier
    assert isinstance(first, OutcomeTier)


def test_roll_band_shifts_odds(engine):
    # Across many turns, Likely yields more successes than Longshot.
    def successes(band):
        n = 0
        for t in range(200):
            engine.state.turn = t
            if engine.roll(band) is OutcomeTier.SUCCESS:
                n += 1
        return n

    assert successes(OddsBand.LIKELY) > successes(OddsBand.LONGSHOT)


def test_apply_deltas_validates_objectives(engine):
    result = engine.apply_deltas(Deltas(complete_objectives=[999]))
    assert any("999" in m for m in result.messages)
    engine.apply_deltas(Deltas(complete_objectives=[1]))
    assert engine.state.objective_status[1] == "Achieved"


def test_auto_objective_fires_when_flags_set(engine):
    # Objective 2 metWhen: flag:creds-reset && flag:host-isolated
    engine.state.flags.update({"creds-reset", "host-isolated"})
    newly = engine.apply_auto_objectives()
    assert 2 in newly
    assert engine.state.objective_status[2] == "Achieved"


def test_opfor_win_ends_as_loss(engine):
    engine.state.opfor_progress = engine.scenario.opfor.win_threshold
    assert engine.check_end() is True
    assert engine.state.outcome == "LOSS"


def test_objectives_met_ends_as_win(engine):
    for o in engine.scenario.objectives:
        engine.state.objective_status[o.id] = "Achieved"
    assert engine.check_end() is True
    assert engine.state.outcome == "WIN"


def test_clock_expiry_ends(engine):
    engine.tick(engine.window_minutes)
    assert engine.minutes_left == 0
    assert engine.check_end() is True
    assert engine.state.outcome in {"WIN", "LOSS", "INCOMPLETE"}
