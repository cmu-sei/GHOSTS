"""Operation OVERLORD scenario branch and prioritization behavior."""

from pathlib import Path

import pytest

from ghosts_rpg.game import Game
from ghosts_rpg.loader import load_bundle_file

FIXTURE = (
    Path(__file__).resolve().parents[1]
    / "fixtures"
    / "scenarios"
    / "operation-overlord.json"
)


@pytest.fixture
def game():
    return Game(load_bundle_file(FIXTURE))


def test_overlord_fixture_loads_player_facing_metadata(game):
    assert game.bundle.catalog is not None
    assert game.bundle.catalog.listed
    assert game.bundle.scenario.name == "OPERATION OVERLORD: The Normandy Decision"
    assert len(game.engine.events) == 9
    assert game.engine.known_flags == {"overlord-plan-approved"}


def test_prioritizing_decision_brief_approves_overlord(game):
    game.start()
    game.act("next")
    game.act("next")
    frame = game.act(
        "task 5: approve and sign the five-division Normandy joint concept"
    )

    assert "overlord-plan-approved" in game.engine.state.flags
    assert frame.hud["containmentContained"] is True

    game.act("task 3: send coordination order")
    frame = game.act("task 4: send coordination order")
    assert any(beat.step_number == 6 for beat in frame.beats)
    assert not any(beat.step_number == 7 for beat in frame.beats)

    frame = game.act("publish the NEPTUNE warning order")
    assert frame.is_complete
    assert frame.aar["outcome"] == "WIN"


def test_unsupported_area_recommendation_is_adjudicated(game):
    game.start()

    frame = game.act("I suggest the brittany coast because i once vacationed there.")

    notices = " ".join(frame.notices)
    assert "Brittany" in notices
    assert "personal preference" in notices
    assert "No adjudicated effect" not in notices
    assert "did nothing here" not in notices
    assert "overlord-plan-approved" not in game.engine.state.flags
    assert frame.tasks[0]["step"] == 4


class FakeJudgeLlm:
    enabled = True

    def __init__(self):
        self.prompts = []

    def generate(self, prompt: str, system: str | None = None) -> str | None:
        self.prompts.append((system, prompt))
        if not system or "Leitung" not in system:
            return None
        return """
        {
          "findings": [
            "LLM judge rejected Brittany: it is outside the current Overlord trade space and the rationale is personal preference."
          ],
          "knowledge": [
            "Brittany recommendation rejected against the common picture."
          ],
          "completeObjectiveIds": [],
          "setFlags": [],
          "resolvesTask": true,
          "minutes": 15
        }
        """


def test_llm_judge_adjudicates_free_text_recommendation():
    llm = FakeJudgeLlm()
    game = Game(load_bundle_file(FIXTURE), llm=llm)
    game.start()

    frame = game.act("I suggest the brittany coast because i once vacationed there.")

    notices = " ".join(frame.notices)
    assert "LLM judge rejected Brittany" in notices
    assert "No adjudicated effect" not in notices
    assert "did nothing here" not in notices
    assert game.engine.state.knowledge == [
        "Brittany recommendation rejected against the common picture."
    ]
    assert "overlord-plan-approved" not in game.engine.state.flags
    assert frame.tasks[0]["step"] == 4
    assert llm.prompts


def test_polishing_annexes_first_misses_command_decision(game):
    game.start()
    game.act("approve Normandy area estimate")
    frame = game.act("approve landing craft and Mulberry sustainment plan")

    assert game.engine.state.detonated
    assert "overlord-plan-approved" not in game.engine.state.flags
    assert any(beat.step_number == 7 for beat in frame.beats)
    assert any("conference closed" in notice.lower() for notice in frame.notices)
