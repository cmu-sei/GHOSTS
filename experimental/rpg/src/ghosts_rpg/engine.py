"""The authoritative world-state engine.

Pure state machine over a ScenarioBundle + WorldState. No fixed timeline: the
engine holds mutable ground truth and answers three questions each turn —

  - what could happen next  -> available_moves() / pending_triggers()
  - resolve the odds        -> roll(band) (hidden, seeded)
  - apply what happened      -> apply_deltas() (validated here)

The judge (judge.py) and OPFOR (opfor.py) only *propose* deltas and choices; the
engine decides what is legal, advances the clock, fires triggers, and computes the
end condition. Branch selection is engine-evaluated, never LLM-chosen.
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass, field
from typing import Optional

from .models import (
    OddsBand,
    OutcomeTier,
    PlaybookMove,
    ScenarioBundle,
    Trigger,
    WorldState,
)


@dataclass
class Deltas:
    """A validated set of state changes proposed by the judge or a move/trigger.

    The engine applies only the parts that reference things the scenario tracks."""

    set_flags: list[str] = field(default_factory=list)
    set_facts: dict[str, str] = field(default_factory=dict)
    complete_objectives: list[int] = field(default_factory=list)
    fail_objectives: list[int] = field(default_factory=list)
    opfor_progress: int = 0
    minutes: int = 0


@dataclass
class ApplyResult:
    messages: list[str] = field(default_factory=list)
    applied: int = 0


# The odds -> outcome-tier distribution. Each band maps to cumulative thresholds on
# a 0.0-1.0 roll: (success_max, partial_max, failure_max); above failure_max is a
# backfire. A stronger band shifts probability mass toward success.
_BANDS: dict[OddsBand, tuple[float, float, float]] = {
    OddsBand.LIKELY: (0.70, 0.90, 0.98),
    OddsBand.EVEN: (0.45, 0.75, 0.93),
    OddsBand.UNLIKELY: (0.20, 0.50, 0.85),
    OddsBand.LONGSHOT: (0.08, 0.30, 0.75),
}


class Engine:
    def __init__(self, bundle: ScenarioBundle, state: Optional[WorldState] = None):
        self.bundle = bundle
        self.scenario = bundle.scenario
        self.state = state or WorldState()
        self._triggers = {t.id: t for t in self.scenario.triggers}
        self._objectives = {o.id: o for o in self.scenario.objectives}
        self.known_flags = self._collect_known_flags()
        # Flags OPFOR sets through its own playbook — the adversary's ground truth.
        # The judge (adjudicating the player's move) must never set these; OPFOR
        # earns its own progress via precondition-gated moves.
        self.opfor_flags = {f for m in self.scenario.opfor.playbook for f in m.set_flags}
        # Flags the judge may set as a consequence of the player's action.
        self.defender_flags = self.known_flags - self.opfor_flags

    # ── lifecycle ────────────────────────────────────────────────────────

    def start(self) -> None:
        """Seed world state from the authored spec."""
        w = self.scenario.world
        if not self.state.flags:
            self.state.flags = set(w.flags)
        if not self.state.facts:
            self.state.facts = dict(w.facts)
        if not self.state.objective_status:
            self.state.objective_status = {o.id: "Active" for o in self.scenario.objectives}

    @property
    def window_minutes(self) -> int:
        return max(1, self.scenario.clock.window_minutes)

    @property
    def tick_minutes(self) -> int:
        return max(0, self.scenario.clock.tick_minutes)

    @property
    def minutes_left(self) -> int:
        return max(0, self.window_minutes - self.state.clock_minutes)

    # ── condition grammar (flag:x / !x / objective:N / clock>=N joined by &&) ──

    def evaluate_condition(self, condition: Optional[str]) -> bool:
        """Empty => always true. An unparseable term gates the condition (False),
        never silently opens it."""
        if condition is None or not condition.strip():
            return True
        for raw in condition.split("&&"):
            term = raw.strip()
            if not term:
                continue
            if not self._eval_term(term):
                return False
        return True

    def _eval_term(self, term: str) -> bool:
        if term.startswith("flag:"):
            return term[len("flag:"):].strip() in self.state.flags
        if term.startswith("!"):
            return term[1:].strip() not in self.state.flags
        if term.startswith("objective:"):
            try:
                oid = int(term[len("objective:"):].strip())
            except ValueError:
                return False
            return self.state.objective_status.get(oid) == "Achieved"
        if term.startswith("clock>="):
            try:
                threshold = int(term[len("clock>="):].strip())
            except ValueError:
                return False
            return self.state.clock_minutes >= threshold
        return False  # unknown term -> gated

    # ── what could happen next ──────────────────────────────────────────

    def available_moves(self) -> list[PlaybookMove]:
        """OPFOR's live menu: playbook moves whose preconditions hold and that have
        not already been played."""
        return [
            m
            for m in self.scenario.opfor.playbook
            if m.id not in self.state.opfor_moves_played
            and self.evaluate_condition(m.preconds)
        ]

    def pending_triggers(self) -> list[Trigger]:
        """Triggers whose condition now holds and that have not yet fired."""
        return [
            t
            for t in self.scenario.triggers
            if t.id not in self.state.fired_triggers and self.evaluate_condition(t.when)
        ]

    # ── resolve the odds (hidden, seeded) ───────────────────────────────

    def roll(self, band: OddsBand) -> OutcomeTier:
        """Resolve a band into an outcome tier with a hidden, deterministic roll.

        Seeded on the turn number + band so a session replays identically and tests
        are stable, while the player cannot see or predict the result."""
        thresholds = _BANDS.get(band, _BANDS[OddsBand.EVEN])
        r = self._seeded_roll(f"{self.state.turn}:{band.value}")
        success_max, partial_max, failure_max = thresholds
        if r <= success_max:
            return OutcomeTier.SUCCESS
        if r <= partial_max:
            return OutcomeTier.PARTIAL
        if r <= failure_max:
            return OutcomeTier.FAILURE
        return OutcomeTier.BACKFIRE

    def _seeded_roll(self, salt: str) -> float:
        """A deterministic float in [0,1). Date/random are unavailable and would
        break replay anyway; a hash of state gives stable per-turn variety."""
        digest = hashlib.sha256(f"{self.scenario.id}:{salt}".encode()).hexdigest()
        return int(digest[:8], 16) / 0xFFFFFFFF

    # ── apply what happened (validation lives here) ─────────────────────

    def apply_deltas(self, deltas: Deltas) -> ApplyResult:
        result = ApplyResult()
        for flag in deltas.set_flags:
            if flag and flag not in self.state.flags:
                self.state.flags.add(flag)
                result.applied += 1
        for key, value in deltas.set_facts.items():
            self.state.facts[key] = value
            result.applied += 1
        for oid in deltas.complete_objectives:
            if oid in self.state.objective_status and self.state.objective_status[oid] != "Achieved":
                self.state.objective_status[oid] = "Achieved"
                result.applied += 1
            elif oid not in self.state.objective_status:
                result.messages.append(f"No effect: objective {oid} is not in this scenario.")
        for oid in deltas.fail_objectives:
            if oid in self.state.objective_status and self.state.objective_status[oid] != "Achieved":
                self.state.objective_status[oid] = "Failed"
                result.applied += 1
        if deltas.opfor_progress:
            self.state.opfor_progress = max(0, self.state.opfor_progress + deltas.opfor_progress)
            result.applied += 1
        return result

    def apply_move(self, move: PlaybookMove) -> None:
        """Apply an OPFOR move's authored deltas to ground truth and log it."""
        self.apply_deltas(
            Deltas(
                set_flags=list(move.set_flags),
                set_facts=dict(move.set_facts),
                opfor_progress=move.progress,
            )
        )
        self.state.opfor_moves_played.append(move.id)

    def fire_trigger(self, trigger: Trigger) -> None:
        self.apply_deltas(
            Deltas(set_flags=list(trigger.set_flags), set_facts=dict(trigger.set_facts))
        )
        self.state.fired_triggers.add(trigger.id)

    # ── clock + objectives ──────────────────────────────────────────────

    def tick(self, minutes: int) -> None:
        """Advance the clock (clamped at the window)."""
        if minutes <= 0:
            return
        self.state.clock_minutes = min(self.window_minutes, self.state.clock_minutes + minutes)

    def apply_auto_objectives(self) -> list[int]:
        """Mark any objective whose met_when condition now holds. Returns newly met
        objective ids so the caller can narrate them."""
        newly: list[int] = []
        for o in self.scenario.objectives:
            if not o.met_when:
                continue
            if self.state.objective_status.get(o.id) == "Achieved":
                continue
            if self.evaluate_condition(o.met_when):
                self.state.objective_status[o.id] = "Achieved"
                newly.append(o.id)
        return newly

    # ── end condition ────────────────────────────────────────────────────

    @property
    def opfor_won(self) -> bool:
        threshold = self.scenario.opfor.win_threshold
        return threshold > 0 and self.state.opfor_progress >= threshold

    @property
    def objectives_met(self) -> bool:
        objs = self.scenario.objectives
        return bool(objs) and all(
            self.state.objective_status.get(o.id) == "Achieved" for o in objs
        )

    def check_end(self) -> bool:
        """Set is_complete + outcome when the game is over. Idempotent.

        Ends when OPFOR wins (LOSS), the player meets every objective (WIN), or the
        clock runs out (WIN if objectives met, else LOSS if OPFOR made progress,
        else INCOMPLETE)."""
        if self.state.is_complete:
            return True
        if self.opfor_won:
            self.state.is_complete = True
            self.state.outcome = "LOSS"
        elif self.objectives_met:
            self.state.is_complete = True
            self.state.outcome = "WIN"
        elif self.minutes_left <= 0:
            self.state.is_complete = True
            if self.objectives_met:
                self.state.outcome = "WIN"
            elif self.state.opfor_progress > 0:
                self.state.outcome = "LOSS"
            else:
                self.state.outcome = "INCOMPLETE"
        return self.state.is_complete

    # ── helpers ───────────────────────────────────────────────────────────

    def _collect_known_flags(self) -> set[str]:
        """Every flag name the scenario references — used to validate proposed
        deltas so the judge can't invent state the scenario doesn't track."""
        flags: set[str] = set(self.scenario.world.flags)
        conditions = [t.when for t in self.scenario.triggers]
        conditions += [m.preconds for m in self.scenario.opfor.playbook]
        conditions += [o.met_when for o in self.scenario.objectives]
        for m in self.scenario.opfor.playbook:
            flags.update(m.set_flags)
        for t in self.scenario.triggers:
            flags.update(t.set_flags)
        for cond in conditions:
            for raw in (cond or "").split("&&"):
                term = raw.strip()
                if term.startswith("flag:"):
                    flags.add(term[len("flag:"):].strip())
                elif term.startswith("!"):
                    flags.add(term[1:].strip())
        flags.discard("")
        return flags
