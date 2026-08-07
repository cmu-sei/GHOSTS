"""The OPFOR agent — the thinking adversary.

Each turn OPFOR looks at the moves currently available to it (playbook entries whose
preconditions hold) and picks one to play toward its objective. This is what makes
the game unsolvable-once: the next adversary event is *chosen at runtime* from a
menu, not pre-wired into a timeline.

LLM-driven when Ollama is configured (it reasons about which move best advances its
objective given what has happened). Offline it degrades to picking the highest-
progress available move — enough to keep pressure on, not the full adversary.
"""

from __future__ import annotations

import json

from .engine import Engine
from .llm import make_llm
from .models import PlaybookMove


class Opfor:
    def __init__(self, engine: Engine, llm=None):
        self.engine = engine
        self.llm = llm or make_llm()

    def choose(self) -> PlaybookMove | None:
        """Pick OPFOR's move this turn, or None if it has no available move."""
        moves = self.engine.available_moves()
        if not moves:
            return None
        chosen_id = self._llm_choose(moves)
        if chosen_id:
            for m in moves:
                if m.id == chosen_id:
                    return m
        return self._offline_choose(moves)

    def _offline_choose(self, moves: list[PlaybookMove]) -> PlaybookMove:
        """Greedy: the available move that advances OPFOR's objective most."""
        return max(moves, key=lambda m: (m.progress, m.id))

    def _llm_choose(self, moves: list[PlaybookMove]) -> str | None:
        if not self.llm.enabled:
            return None
        raw = self.llm.generate(
            self._choose_prompt(moves),
            system=(
                "You are OPFOR, the adversary commander in a staff exercise. Choose "
                "the single move from your available playbook that best advances your "
                "objective given what has happened. Consider tempo and what the "
                "defender has done. Return ONLY JSON: "
                '{"moveId":"<one of the listed ids>"}.'
            ),
        )
        data = _parse_object(raw)
        if not data:
            return None
        move_id = str(data.get("moveId", "")).strip()
        return move_id or None

    def _choose_prompt(self, moves: list[PlaybookMove]) -> str:
        sc = self.engine.scenario
        return json.dumps(
            {
                "opforObjective": sc.opfor.objective,
                "progress": self.engine.state.opfor_progress,
                "winThreshold": sc.opfor.win_threshold,
                "movesPlayed": self.engine.state.opfor_moves_played,
                "worldFacts": self.engine.state.facts,
                "availableMoves": [
                    {"id": m.id, "domain": m.domain, "description": m.description,
                     "progress": m.progress}
                    for m in moves
                ],
            },
            ensure_ascii=True,
            indent=2,
        )


def _parse_object(raw: str | None) -> dict | None:
    if not raw:
        return None
    text = raw.strip()
    start, end = text.find("{"), text.rfind("}")
    if start == -1 or end == -1 or end < start:
        return None
    try:
        data = json.loads(text[start : end + 1])
    except json.JSONDecodeError:
        return None
    return data if isinstance(data, dict) else None
