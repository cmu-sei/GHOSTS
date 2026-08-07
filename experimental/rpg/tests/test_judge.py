"""Judge: offline band heuristic, tier resolution, delta validation."""

from pathlib import Path

import pytest

from ghosts_rpg.engine import Engine
from ghosts_rpg.judge import Judge
from ghosts_rpg.loader import load_bundle_file
from ghosts_rpg.models import OddsBand, OutcomeTier

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "meridian-hybrid.json"


@pytest.fixture
def judge():
    e = Engine(load_bundle_file(FIXTURE))
    e.start()
    return Judge(e, llm=None)  # offline


def test_sound_reasoned_move_earns_better_band(judge):
    strong = judge.assess(
        "isolate the affected host and reset the finance credentials",
        "because cutting the foothold denies lateral movement toward the file share",
    )
    weak = judge.assess("do something", "")
    assert strong.band is OddsBand.LIKELY
    assert weak.band in {OddsBand.UNLIKELY, OddsBand.LONGSHOT}


def test_rash_move_is_longshot(judge):
    a = judge.assess("hack back at their server", "because I want to nuke them")
    assert a.band is OddsBand.LONGSHOT


def test_empty_action_is_longshot(judge):
    assert judge.assess("", "").band is OddsBand.LONGSHOT


def test_resolve_success_completes_matched_objective_and_gates(judge):
    # "deny the intrusion" objective 2 is gated on creds-reset && host-isolated.
    narration, deltas = judge.resolve(
        "reset credentials and isolate the affected host to deny the intrusion",
        OutcomeTier.SUCCESS,
    )
    assert narration
    assert "creds-reset" in deltas.set_flags
    assert "host-isolated" in deltas.set_flags
    assert 2 in deltas.complete_objectives


def test_resolve_backfire_helps_opfor(judge):
    _, deltas = judge.resolve("issue a public denial", OutcomeTier.BACKFIRE)
    assert deltas.opfor_progress >= 1


def test_resolve_failure_changes_nothing(judge):
    _, deltas = judge.resolve("contain the threat", OutcomeTier.FAILURE)
    assert not deltas.set_flags
    assert not deltas.complete_objectives
    assert deltas.opfor_progress == 0


class _StubLLM:
    """An LLM that returns a fixed payload — used to exercise the LLM resolve path."""

    enabled = True

    def __init__(self, payload: str):
        self._payload = payload

    def generate(self, prompt, system=None):
        return self._payload


def test_llm_resolve_cannot_set_opfor_flags_or_advance_opfor():
    """The judge adjudicates the PLAYER's move: even if the model tries to hand the
    adversary ground truth, the engine's flag partition and progress cap strip it.
    This is the one-turn-loss bug guard."""
    engine = Engine(load_bundle_file(FIXTURE))
    engine.start()
    # A hostile/confused ruling: set OPFOR advancement flags and jump progress on a
    # mere Partial, plus one legitimate defender flag.
    payload = (
        '{"narration":"The situation shifts.",'
        '"setFlags":["narrative-seeded","near-fileshare","ransom-staged","creds-reset"],'
        '"completeObjectiveIds":[],"failObjectiveIds":[],"opforProgress":3}'
    )
    judge = Judge(engine, llm=_StubLLM(payload))
    _, deltas = judge.resolve("investigate the anomaly", OutcomeTier.PARTIAL)
    # OPFOR's own flags are rejected; only the defender flag survives.
    assert "creds-reset" in deltas.set_flags
    assert not (set(deltas.set_flags) & engine.opfor_flags)
    # A Partial ruling never advances the adversary.
    assert deltas.opfor_progress == 0


def test_llm_resolve_backfire_advances_opfor_by_one():
    payload = '{"narration":"It backfires.","setFlags":[],"opforProgress":3}'
    engine = Engine(load_bundle_file(FIXTURE))
    engine.start()
    judge = Judge(engine, llm=_StubLLM(payload))
    _, deltas = judge.resolve("issue a public denial", OutcomeTier.BACKFIRE)
    # Even if the model asks for +3, a backfire yields exactly +1.
    assert deltas.opfor_progress == 1
