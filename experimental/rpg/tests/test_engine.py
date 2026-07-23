"""Engine: condition grammar, branch selection, effect validation."""

from pathlib import Path

import pytest

from ghosts_rpg.engine import Effect, EffectKind, Engine, Proposal
from ghosts_rpg.loader import load_bundle_file

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "phishing-drill.json"


@pytest.fixture
def engine():
    return Engine(load_bundle_file(FIXTURE))


def test_condition_grammar(engine):
    assert engine.evaluate_condition(None) is True
    assert engine.evaluate_condition("") is True
    assert engine.evaluate_condition("!contained") is True  # flag not yet set
    assert engine.evaluate_condition("flag:contained") is False
    engine.state.flags.add("contained")
    assert engine.evaluate_condition("flag:contained") is True
    assert engine.evaluate_condition("!contained") is False


def test_condition_objective_and_compound(engine):
    assert engine.evaluate_condition("objective:2") is False
    engine.state.objective_status[2] = "Achieved"
    engine.state.flags.add("contained")
    assert engine.evaluate_condition("flag:contained && objective:2") is True
    assert engine.evaluate_condition("flag:contained && objective:1") is False


def test_unparseable_term_gates(engine):
    assert engine.evaluate_condition("garbage") is False
    assert engine.evaluate_condition("objective:notanint") is False


def test_start_points_at_first_event(engine):
    engine.start()
    assert engine.current_event().number == 1


def test_branch_selects_contained_path(engine):
    """With 'contained' set, advancing past step 4 picks step 5, skipping 6."""
    engine.start()
    while engine.current_event() and engine.current_event().number < 4:
        engine.advance()
    engine.state.flags.add("contained")
    nxt = engine.advance()  # from 4
    assert nxt.number == 5


def test_branch_selects_breach_path(engine):
    """Without 'contained', advancing past step 4 picks step 6, skipping 5."""
    engine.start()
    while engine.current_event() and engine.current_event().number < 4:
        engine.advance()
    nxt = engine.advance()  # from 4, no flag
    assert nxt.number == 6


def test_positive_flags_ahead(engine):
    engine.start()
    while engine.current_event() and engine.current_event().number < 4:
        engine.advance()
    # Between step 4 and the next player turn (7), step 5 references flag:contained.
    assert engine.positive_flags_ahead() == ["contained"]


def test_setflag_validation(engine):
    ok, msg = engine._apply_effect(Effect(EffectKind.SET_FLAG, "contained"))
    assert ok and "contained" in engine.state.flags
    ok, msg = engine._apply_effect(Effect(EffectKind.SET_FLAG, "not_a_real_flag"))
    assert not ok and "No effect" in msg


def test_complete_objective_validation(engine):
    ok, _ = engine._apply_effect(Effect(EffectKind.COMPLETE_OBJECTIVE, 1))
    assert ok and engine.state.objective_status[1] == "Achieved"
    ok, msg = engine._apply_effect(Effect(EffectKind.COMPLETE_OBJECTIVE, 999))
    assert not ok and "unknown objective" in msg
