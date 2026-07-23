"""The authoritative game engine.

Pure state machine over a ScenarioBundle + GameState. It owns:

- the step pointer (walk timeline events in Number order),
- the condition grammar (flag:x / !x / objective:N / &&),
- forward branch selection (lowest-Number later step whose condition holds),
- validation of proposed effects against the loaded scenario.

The Dungeon Master (dm.py) may only *propose* effects; the engine decides what is
legal and which branch fires. The engine never narrates and never calls the DM —
control flow is its own. Branching is engine-evaluated, never LLM-chosen.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import Optional

from .models import GameState, ScenarioBundle, TimelineEvent


class EffectKind(str, Enum):
    SET_FLAG = "setFlag"
    COMPLETE_OBJECTIVE = "completeObjective"
    ADD_KNOWLEDGE = "addKnowledge"
    ADD_INVENTORY = "addInventory"
    ADVANCE = "advanceStep"


@dataclass
class Effect:
    kind: EffectKind
    value: str | int = ""


@dataclass
class Proposal:
    """One player action, decomposed into atomic effects the engine validates.

    The DM produces this; the engine adjudicates. `resolves_task` tells the
    orchestration whether this action clears the targeted worklist task (recon is
    "soft" and leaves the task open). `minutes` is what the action costs the lunch
    clock."""

    action_label: str
    effects: list[Effect] = field(default_factory=list)
    resolves_task: bool = False
    minutes: int = 0


@dataclass
class ApplyResult:
    messages: list[str] = field(default_factory=list)
    applied: int = 0

    @property
    def any_applied(self) -> bool:
        return self.applied > 0


class Engine:
    def __init__(self, bundle: ScenarioBundle, state: Optional[GameState] = None):
        self.bundle = bundle
        self.state = state or GameState()
        self.events: list[TimelineEvent] = sorted(
            (bundle.scenario.timeline.events if bundle.scenario.timeline else []),
            key=lambda e: e.number,
        )
        self._by_num = {e.number: e for e in self.events}
        self.known_flags = self._collect_known_flags()
        # The lunch clock: the exercise window in minutes. The player is racing to
        # clear the worklist before this runs out (DESIGN: priority + urgency).
        mech = bundle.scenario.game_mechanics
        self.lunch_minutes = max(1, int(round((mech.duration_hours if mech else 1.0) * 60)))
        # The fuse: minutes on the lunch clock by which the steering flag(s) must be
        # set or the threat detonates onto the loss branch. None => no deadline.
        self.containment_deadline = mech.containment_deadline_minutes if mech else None
        # Seed objective statuses from the scenario.
        if not self.state.objective_status:
            self.state.objective_status = {o.id: o.status for o in bundle.objectives}

    # ── condition grammar ──────────────────────────────────────────────

    def evaluate_condition(self, condition: Optional[str]) -> bool:
        """flag:x / !x / objective:N joined by &&. Empty => open (no gate).
        An unparseable term gates the step (returns False) — never silently open."""
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
                return False  # unparseable -> gated
            return self.state.objective_status.get(oid) == "Achieved"
        return False  # unknown term -> gated

    # ── step pointer / branch selection ────────────────────────────────

    def start(self) -> None:
        first = self._eligible_after(0)
        if first is None:
            self.state.is_complete = True
        else:
            self.state.current_step = first.number

    def current_event(self) -> Optional[TimelineEvent]:
        if self.state.is_complete:
            return None
        return self._by_num.get(self.state.current_step)

    def advance(self) -> Optional[TimelineEvent]:
        """Select the next eligible step. Branch events whose condition is false
        are skipped permanently (we only ever look forward)."""
        nxt = self._eligible_after(self.state.current_step)
        if nxt is None:
            self.state.is_complete = True
            return None
        self.state.current_step = nxt.number
        return nxt

    def _eligible_after(self, cursor: int) -> Optional[TimelineEvent]:
        candidates = [
            e
            for e in self.events
            if e.number > cursor and self.evaluate_condition(e.trigger_condition)
        ]
        return min(candidates, key=lambda e: e.number) if candidates else None

    # ── worklist (parallel open tasks) ─────────────────────────────────

    def open_worklist(self) -> list[TimelineEvent]:
        """The set of Blue-Team tasks open right now, cleared in any order.

        Starting at the current step, this is the maximal contiguous run of
        eligible Blue-Team events (skipping any whose TriggerCondition is unmet),
        minus the ones already resolved this worklist. When the current step is a
        computer-owned event the worklist is empty (the DM is still playing)."""
        cur = self.current_event()
        if cur is None or not cur.is_player_turn:
            return []
        tasks: list[TimelineEvent] = []
        for e in self.events:
            if e.number < cur.number:
                continue
            if not self.evaluate_condition(e.trigger_condition):
                continue
            if not e.is_player_turn:
                break  # a computer beat ends the run of open player tasks
            if e.number not in self.state.cleared_steps:
                tasks.append(e)
        return tasks

    def clear_task(self, step_number: int) -> None:
        self.state.cleared_steps.add(step_number)

    # ── revealed tickets (tickets arrive one at a time) ────────────────

    def revealed_worklist(self) -> list[TimelineEvent]:
        """The open tasks currently *surfaced* to the player, in order.

        Tickets arrive one at a time: by default only the first open task is
        revealed. Tabling reveals the next one without clearing the current, and
        resolving a task auto-reveals the next — so the player can stack up to the
        whole worklist, but doesn't start with it. The full open worklist stays
        authoritative for the fuse, branch advance, and flag ownership."""
        open_tasks = self.open_worklist()
        if not open_tasks:
            return []
        revealed = [e for e in open_tasks if e.number in self.state.revealed_steps]
        if not revealed:  # always surface at least the first open ticket
            first = open_tasks[0]
            self.state.revealed_steps.add(first.number)
            revealed = [first]
        return revealed

    def reveal_next(self) -> Optional[TimelineEvent]:
        """Surface the next open ticket not yet revealed ('table' the current one).
        Returns the newly revealed task, or None if every open ticket is showing."""
        self.revealed_worklist()  # ensure the first ticket is seeded
        for e in self.open_worklist():
            if e.number not in self.state.revealed_steps:
                self.state.revealed_steps.add(e.number)
                return e
        return None

    def has_unrevealed_tasks(self) -> bool:
        return any(e.number not in self.state.revealed_steps for e in self.open_worklist())

    def spend_time(self, minutes: int) -> None:
        """Burn minutes off the lunch clock (clamped at the window)."""
        if minutes <= 0:
            return
        self.state.minutes_spent = min(self.lunch_minutes, self.state.minutes_spent + minutes)

    @property
    def minutes_left(self) -> int:
        return max(0, self.lunch_minutes - self.state.minutes_spent)

    # ── the containment fuse ────────────────────────────────────────────

    @property
    def is_contained(self) -> bool:
        """True once every steering flag the scenario tracks is set — the same
        'contained' test scoring uses to award the win."""
        return bool(self.known_flags) and self.known_flags <= self.state.flags

    def fuse_blown(self) -> bool:
        """The deadline has passed with the threat still un-contained. A no-op when
        the scenario sets no deadline, once already contained, or once detonated."""
        return (
            self.containment_deadline is not None
            and not self.state.detonated
            and not self.is_contained
            and self.state.minutes_spent >= self.containment_deadline
        )

    def detonate(self) -> Optional[TimelineEvent]:
        """The fuse blew: the threat detonates. Abandon the open worklist, lock the
        loss branch, and jump the step pointer onto the first computer event past the
        worklist run — which, with the steering flag unset, is the loss branch."""
        self.state.detonated = True
        # Walk to the end of the current contiguous Blue-Team worklist run so we
        # advance past every abandoned ticket, not just the current one.
        last_blue = self.state.current_step
        for e in self.events:
            if e.number < self.state.current_step:
                continue
            if not self.evaluate_condition(e.trigger_condition):
                continue
            if not e.is_player_turn:
                break
            last_blue = e.number
        self.state.cleared_steps.clear()
        self.state.revealed_steps.clear()
        nxt = self._eligible_after(last_blue)
        if nxt is None:
            self.state.is_complete = True
            return None
        self.state.current_step = nxt.number
        return nxt

    def out_of_time(self) -> bool:
        """The lunch clock is exhausted — the morning is over regardless of the queue."""
        return self.minutes_left <= 0

    def advance_past_worklist(self) -> Optional[TimelineEvent]:
        """Called once the open worklist is empty: jump the step pointer past the
        last cleared task onto the next eligible (computer) event, then reset the
        per-worklist cleared set."""
        last = max(self.state.cleared_steps) if self.state.cleared_steps else self.state.current_step
        self.state.cleared_steps.clear()
        self.state.revealed_steps.clear()
        nxt = self._eligible_after(last)
        if nxt is None:
            self.state.is_complete = True
            return None
        self.state.current_step = nxt.number
        return nxt

    def flags_owned_by(self, event: TimelineEvent) -> list[str]:
        """The forward branch flags this specific task is responsible for setting.

        With several tasks open at once, a decisive action on one task must not
        satisfy another task's branch. A task owns a forward flag when the flag's
        name appears in the success criteria/description of an objective the task
        references — the data's own link between a task and the branch it steers."""
        candidates = self.positive_flags_ahead()
        if not candidates:
            return []
        # A lone open task has no sibling to conflict with — it owns the branch.
        if len(self.open_worklist()) <= 1:
            return candidates
        by_id = {o.id: o for o in self.bundle.objectives}
        text = " ".join(
            f"{o.name} {o.description} {o.success_criteria}".lower()
            for oid in event.objective_ids
            if (o := by_id.get(oid))
        )
        return [f for f in candidates if f.lower() in text]

    def positive_flags_ahead(self) -> list[str]:
        """Flags referenced positively (flag:x) on the computer branch run that
        follows the current worklist. A decisive player action sets these to steer
        the timeline onto the favorable branch; doing nothing falls to the !x branch.

        Sibling player tasks in the open worklist are skipped; collection stops at
        the next player turn after that branch run."""
        out: list[str] = []
        seen_computer = False
        for e in self.events:
            if e.number <= self.state.current_step:
                continue
            if e.is_player_turn:
                if seen_computer:
                    break
                continue  # another open worklist task — not a branch event
            seen_computer = True
            for raw in (e.trigger_condition or "").split("&&"):
                term = raw.strip()
                if term.startswith("flag:"):
                    flag = term[len("flag:"):].strip()
                    if flag and flag not in out:
                        out.append(flag)
        return out

    # ── effect application (validation lives here) ──────────────────────

    def apply(self, proposal: Proposal) -> ApplyResult:
        result = ApplyResult()
        for eff in proposal.effects:
            ok, msg = self._apply_effect(eff)
            if ok:
                result.applied += 1
            if msg:
                result.messages.append(msg)
        return result

    def _apply_effect(self, eff: Effect) -> tuple[bool, str]:
        if eff.kind is EffectKind.SET_FLAG:
            flag = str(eff.value)
            if flag not in self.known_flags:
                return False, f"No effect: '{flag}' isn't something this scenario tracks."
            self.state.flags.add(flag)
            return True, ""
        if eff.kind is EffectKind.COMPLETE_OBJECTIVE:
            try:
                oid = int(eff.value)
            except (TypeError, ValueError):
                return False, "No effect: unknown objective."
            if oid not in self.state.objective_status:
                return False, "No effect: unknown objective."
            self.state.objective_status[oid] = "Achieved"
            return True, ""
        if eff.kind is EffectKind.ADD_KNOWLEDGE:
            self.state.knowledge.append(str(eff.value))
            return True, ""
        if eff.kind is EffectKind.ADD_INVENTORY:
            self.state.inventory.append(str(eff.value))
            return True, ""
        if eff.kind is EffectKind.ADVANCE:
            return True, ""
        return False, "No effect."

    # ── helpers ─────────────────────────────────────────────────────────

    def _collect_known_flags(self) -> set[str]:
        flags: set[str] = set()
        for e in self.events:
            for raw in (e.trigger_condition or "").split("&&"):
                term = raw.strip()
                if term.startswith("flag:"):
                    flags.add(term[len("flag:"):].strip())
                elif term.startswith("!"):
                    flags.add(term[1:].strip())
        flags.discard("")
        return flags
