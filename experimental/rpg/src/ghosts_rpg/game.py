"""Game orchestration — the Kriegspiel turn loop.

One player turn:

  1. player submits action + rationale
  2. judge.assess         -> odds band + critique          (matrix-game half)
  3. engine.roll          -> hidden outcome tier
  4. judge.resolve        -> narration + proposed deltas    (free-Kriegspiel half)
  5. engine.apply_deltas  -> validated state change
  6. opfor.choose/apply   -> the adversary reacts, mutating ground truth
  7. fog.visible          -> the player perceives only emitted indicators
  8. engine.tick + triggers + auto-objectives
  9. engine.check_end     -> AAR when the game is over

Returns a Frame the UI (terminal or Angular) renders. The engine is authoritative
for all state; the judge and OPFOR only propose.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Optional

from .engine import Engine
from .fog import reveals_ground_truth, visible_indicators
from .judge import Judge
from .llm import make_llm
from .models import ScenarioBundle, TranscriptEntry, TurnRecord
from .opfor import Opfor
from .scoring import Aar, review


@dataclass
class Beat:
    """One narrated line. `speaker` drives colour in the UI."""

    turn: int
    speaker: str  # Judge | OPFOR | Control | Player
    text: str


@dataclass
class Frame:
    """What changed since the last interaction, plus the current picture."""

    beats: list[Beat] = field(default_factory=list)
    awaiting_player: bool = False
    band: Optional[str] = None  # odds band the judge assigned this turn
    critique: str = ""  # judge's one-line critique of the player's reasoning
    tier: Optional[str] = None  # resolved outcome tier this turn
    notices: list[str] = field(default_factory=list)
    is_complete: bool = False
    aar: Optional[dict] = None
    hud: dict = field(default_factory=dict)


class Game:
    def __init__(self, bundle: ScenarioBundle, llm=None):
        self.bundle = bundle
        self.engine = Engine(bundle)
        llm = llm or make_llm()
        self.judge = Judge(self.engine, llm=llm)
        self.opfor = Opfor(self.engine, llm=llm)
        self.llm = llm

    # ── lifecycle ────────────────────────────────────────────────────────

    def start(self) -> Frame:
        self.engine.start()
        sc = self.bundle.scenario
        beats = [Beat(0, "Control", sc.situation or sc.description)]
        self._record("Control", sc.situation or sc.description)
        # Any trigger/objective already true at T+0 fires before the first player move.
        beats += self._resolve_world()
        if self.engine.check_end():
            return self._frame(beats=beats, complete=True)
        return self._frame(beats=beats, awaiting=True)

    def act(self, action: str, rationale: str = "") -> Frame:
        if self.engine.state.is_complete:
            return self._frame(notices=["The exercise is over."], complete=True)
        action = action.strip()
        if not action:
            return self._frame(awaiting=True, notices=["State an action to adjudicate."])

        self.engine.state.turn += 1
        turn = self.engine.state.turn
        self._record("Player", f"{action}" + (f" — because {rationale}" if rationale else ""))

        # Phase A: assess -> band. Phase B: hidden roll -> tier -> narration + deltas.
        assessment = self.judge.assess(action, rationale)
        tier = self.engine.roll(assessment.band)
        narration, deltas = self.judge.resolve(action, tier)
        result = self.engine.apply_deltas(deltas)
        self.engine.tick(deltas.minutes)

        beats = [Beat(turn, "Judge", narration)]
        self._record("Judge", narration)
        self.engine.state.rulings.append(
            TurnRecord(
                turn=turn,
                action=action,
                rationale=rationale,
                band=assessment.band.value,
                tier=tier.value,
                critique=assessment.critique,
            )
        )

        notices = list(result.messages)

        # The adversary reacts, then the world resolves (triggers, auto-objectives),
        # then we check for the end.
        if not self.engine.check_end():
            beats += self._opfor_turn()
        if not self.engine.state.is_complete:
            beats += self._resolve_world()
        complete = self.engine.check_end()
        return self._frame(
            beats=beats,
            awaiting=not complete,
            band=assessment.band.value,
            critique=assessment.critique,
            tier=tier.value,
            notices=notices,
            complete=complete,
        )

    # ── the adversary's turn ─────────────────────────────────────────────

    def _opfor_turn(self) -> list[Beat]:
        move = self.opfor.choose()
        if move is None:
            return []
        self.engine.apply_move(move)
        seen = visible_indicators(self.bundle.scenario, move.indicators)
        for ind in seen:
            if ind not in self.engine.state.indicators_seen:
                self.engine.state.indicators_seen.append(ind)
        if not seen:
            return []  # OPFOR acted, but nothing surfaced to the player (fog)
        # The adversary is hidden: the player never sees OPFOR's move, only what
        # their own sensors pick up. Surface it as neutral intelligence, not as
        # the enemy speaking.
        text = "New indicators: " + "; ".join(seen)
        self._record("Signals", text)
        return [Beat(self.engine.state.turn, "Signals", text)]

    # ── scheduled world resolution (triggers + auto-objectives) ──────────

    def _resolve_world(self) -> list[Beat]:
        beats: list[Beat] = []
        for trigger in self.engine.pending_triggers():
            self.engine.fire_trigger(trigger)
            seen = visible_indicators(self.bundle.scenario, trigger.indicators)
            for ind in seen:
                if ind not in self.engine.state.indicators_seen:
                    self.engine.state.indicators_seen.append(ind)
            text = trigger.inject
            if seen:
                text = f"{text} ({'; '.join(seen)})" if text else "; ".join(seen)
            if text:
                self._record("Control", text)
                beats.append(Beat(self.engine.state.turn, "Control", text))
        for oid in self.engine.apply_auto_objectives():
            obj = next((o for o in self.bundle.scenario.objectives if o.id == oid), None)
            if obj:
                text = f"Objective met: {obj.name}."
                self._record("Control", text)
                beats.append(Beat(self.engine.state.turn, "Control", text))
        return beats

    # ── bookkeeping ───────────────────────────────────────────────────────

    def _record(self, speaker: str, text: str) -> None:
        self.engine.state.transcript.append(
            TranscriptEntry(turn=self.engine.state.turn, speaker=speaker, text=text)
        )

    def _frame(
        self,
        beats=None,
        awaiting=False,
        band=None,
        critique="",
        tier=None,
        notices=None,
        complete=False,
    ) -> Frame:
        complete = complete or self.engine.state.is_complete
        aar: Aar | None = review(self.engine) if complete else None
        return Frame(
            beats=beats or [],
            awaiting_player=awaiting and not complete,
            band=band,
            critique=critique,
            tier=tier,
            notices=notices or [],
            is_complete=complete,
            aar=asdict(aar) if aar else None,
            hud=self.hud(),
        )

    def hud(self) -> dict:
        sc = self.bundle.scenario
        st = self.engine.state
        objs = [
            {
                "id": o.id,
                "name": o.name,
                "status": st.objective_status.get(o.id, "Active"),
                "met": st.objective_status.get(o.id) == "Achieved",
            }
            for o in sc.objectives
        ]
        hud = {
            "scenario": sc.name,
            "role": sc.player.role,
            "mandate": sc.player.mandate,
            "objectives": objs,
            "indicators": list(st.indicators_seen),
            "turn": st.turn,
            "minutesLeft": self.engine.minutes_left,
            "windowMinutes": self.engine.window_minutes,
            "clockLabel": sc.clock.label,
            "opforName": sc.opfor.name,
            "opforProgress": st.opfor_progress,
            "opforThreshold": sc.opfor.win_threshold,
            "threats": [sc.opfor.name] if sc.opfor.name else [],
        }
        # Under fog, ground truth stays hidden; training/debug scenarios may show it.
        if reveals_ground_truth(sc):
            hud["flags"] = sorted(st.flags)
            hud["facts"] = dict(st.facts)
        return hud
