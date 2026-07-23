"""The Dungeon Master.

The computer plays everyone but the player. The DM:
  - narrates each timeline event (Red/White/Green = OPFOR & exercise control),
  - at a Blue Team worklist, presents each open task + a *varied* menu of actions,
  - parses the player's choice (an offered action OR free text) into ONE Proposal
    against a specific open task.

When an Ollama model is configured the DM proposes a scenario-specific action menu
through it; with no model present a deterministic scenario-derived menu keeps the
game fully playable offline.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from .engine import Effect, EffectKind, Engine, Proposal
from .llm import OllamaClient
from .models import TimelineEvent

# Intent keyword maps for the offline free-text parser.
_INVESTIGATE = ("investigat", "look", "examine", "analy", "triage", "inspect", "recon", "check", "scope", "confirm", "review", "read")
_CONTAIN = (
    "contain", "quarantin", "block", "isolat", "remediat", "reset", "respond",
    "defend", "stop", "kill", "disable", "close", "resolve", "handle", "fix",
    "clear", "ship", "deliver", "approve", "sign", "publish",
)
_NOTIFY = ("notif", "warn", "alert", "tell", "email", "message", "inform", "escalat", "call", "ping", "loop in")
_WAIT = ("wait", "watch", "hold", "stand", "nothing", "ignore", "monitor", "observe", "defer", "punt", "skip", "later")

# Objective classification keywords.
_DETECT_OBJ = ("detect", "investigat", "recogn", "triage", "identif", "scope", "confirm")
_CONTAIN_OBJ = ("contain", "block", "quarantin", "isolat", "remediat", "reset", "stop", "prevent")

# Default minute costs per intent (the lunch-clock economy). Investigating is cheap
# and keeps your turn; resolving a task is the expensive, decisive move.
_COST = {"investigate": 5, "contain": 15, "notify": 5, "wait": 10}


@dataclass
class Action:
    """One offered action on a task: a short label plus the intent it maps to."""

    label: str
    intent: str  # investigate | contain | notify | wait


@dataclass
class Task:
    """One open worklist item the player can pick and clear in any order."""

    step_number: int
    time: str
    prompt: str
    actions: list[Action]


@dataclass
class Turn:
    """The player's current worklist: several open tasks, each with its own menu."""

    tasks: list[Task]
    minutes_left: int


class DungeonMaster:
    def __init__(self, engine: Engine, llm: OllamaClient | None = None):
        self.engine = engine
        self.llm = llm or OllamaClient()

    # ── narration ───────────────────────────────────────────────────────

    def narrate(self, event: TimelineEvent) -> str:
        base = event.description.strip()
        enriched = self._llm_narrate(event, base)
        return enriched or base

    def _llm_narrate(self, event: TimelineEvent, base: str) -> str | None:
        if not self.llm.enabled:
            return None
        sc = self.engine.bundle.scenario
        system = (
            "You are the Dungeon Master of an umpired staff exercise. Narrate vividly "
            "in second person, 2-4 sentences. Do NOT offer choices or ask questions; "
            "the engine handles options. Stay strictly faithful to the facts given."
        )
        prompt = (
            f"Scenario: {sc.name}. {sc.description}\n"
            f"Current event ({event.assigned}, {event.time}): {base}\n"
            "Narrate this beat."
        )
        return self.llm.generate(prompt, system=system)

    # ── the worklist the player faces ───────────────────────────────────

    def worklist(self) -> Turn:
        """The tickets currently surfaced to the player — one at a time by default,
        more as they table or resolve them (engine.revealed_worklist)."""
        tasks = [self._task_for(e) for e in self.engine.revealed_worklist()]
        return Turn(tasks=tasks, minutes_left=self.engine.minutes_left)

    def _task_for(self, event: TimelineEvent) -> Task:
        return Task(
            step_number=event.number,
            time=event.time,
            prompt=self.narrate(event),
            actions=self._actions_for(event),
        )

    def _actions_for(self, event: TimelineEvent) -> list[Action]:
        """A varied, task-specific menu. Try the model first (each proposal is still
        validated by intent); fall back to a deterministic scenario-derived menu."""
        return self._llm_actions(event) or self._fallback_actions(event)

    def _llm_actions(self, event: TimelineEvent) -> list[Action] | None:
        if not self.llm.enabled:
            return None
        sc = self.engine.bundle.scenario
        system = (
            "You propose concrete actions the player could take on ONE task in an "
            "umpired staff exercise. Return ONLY a JSON array of 3-5 objects, "
            'each {"label": "<short imperative action>", "intent": "<one of: '
            'investigate, contain, notify, wait>"}. "investigate" = gather info without '
            'committing; "contain" = the decisive action that resolves the task; '
            '"notify" = tell/escalate to someone; "wait" = defer. Labels must be '
            "specific to the task and scenario. No prose, JSON only."
        )
        prompt = (
            f"Scenario: {sc.name}. {sc.description}\n"
            f"Task ({event.time}): {event.description}\n"
            "Propose the action menu."
        )
        raw = self.llm.generate(prompt, system=system)
        return self._parse_actions(raw)

    @staticmethod
    def _parse_actions(raw: str | None) -> list[Action] | None:
        if not raw:
            return None
        text = raw.strip()
        start, end = text.find("["), text.rfind("]")
        if start == -1 or end == -1 or end < start:
            return None
        try:
            data = json.loads(text[start : end + 1])
        except json.JSONDecodeError:
            return None
        actions: list[Action] = []
        valid = {"investigate", "contain", "notify", "wait"}
        for item in data if isinstance(data, list) else []:
            if not isinstance(item, dict):
                continue
            label = str(item.get("label", "")).strip()
            intent = str(item.get("intent", "")).strip().lower()
            if label and intent in valid:
                actions.append(Action(label=label, intent=intent))
        return actions or None

    def _fallback_actions(self, event: TimelineEvent) -> list[Action]:
        """Deterministic menu derived from the task text — offline-safe.

        Labels are phrased like staff-exercise products, but keep the same intents
        so terse commands and existing saved sessions still work.
        """
        mechanics = self.engine.bundle.scenario.game_mechanics
        decisive = (
            mechanics.decisive_action_label
            if mechanics
            else "issue containment order"
        )
        return [
            Action(label="draft estimate", intent="investigate"),
            Action(label=decisive, intent="contain"),
            Action(label="send coordination order", intent="notify"),
            Action(label="accept risk and defer", intent="wait"),
        ]

    # ── parsing player input -> (target task, Proposal) ──────────────────

    def interpret(self, player_input: str, event: TimelineEvent) -> Proposal:
        """Map free text or an offered action label to ONE proposal on `event`.

        Recognizes an offered action's exact label first, then falls back to the
        keyword intent map so typed free text still lands on a legal effect."""
        text = player_input.strip().lower()
        intent = self._match_action(text, event) or self._classify(text)
        if intent == "investigate":
            return self._investigate(event, player_input)
        if intent == "contain":
            return self._contain(event, player_input)
        if intent == "notify":
            return Proposal(
                action_label=player_input.strip() or "notify",
                effects=[Effect(EffectKind.ADD_KNOWLEDGE, f"Notified the team about: {event.description[:80]}")],
                resolves_task=True,
                minutes=_COST["notify"],
            )
        if intent == "wait":
            return Proposal(action_label=player_input.strip() or "wait", effects=[], resolves_task=True, minutes=_COST["wait"])
        # Unrecognized -> a no-op the engine reports as "No effect".
        return Proposal(
            action_label=player_input.strip() or "(nothing)",
            effects=[],
            resolves_task=False,
        )

    def _match_action(self, text: str, event: TimelineEvent) -> str | None:
        """If the input matches an offered action label for this task, use its intent."""
        for a in self._actions_for(event):
            if text == a.label.strip().lower():
                return a.intent
        return None

    def _classify(self, text: str) -> str | None:
        if any(k in text for k in _CONTAIN):
            return "contain"
        if any(k in text for k in _INVESTIGATE):
            return "investigate"
        if any(k in text for k in _NOTIFY):
            return "notify"
        if any(k in text for k in _WAIT):
            return "wait"
        return None

    # ── intent -> effects (recon is "soft", resolving is decisive) ───────

    def _investigate(self, event: TimelineEvent, label: str) -> Proposal:
        effects: list[Effect] = [Effect(EffectKind.ADD_KNOWLEDGE, self._recon_fact(event))]
        for oid in self._objectives_matching(event, _DETECT_OBJ):
            effects.append(Effect(EffectKind.COMPLETE_OBJECTIVE, oid))
        # Recon does not resolve the task; the player can still act decisively.
        return Proposal(action_label=label.strip() or "investigate", effects=effects, resolves_task=False, minutes=_COST["investigate"])

    def _contain(self, event: TimelineEvent, label: str) -> Proposal:
        effects: list[Effect] = []
        for flag in self.engine.flags_owned_by(event):
            effects.append(Effect(EffectKind.SET_FLAG, flag))
        for oid in self._objectives_matching(event, _CONTAIN_OBJ):
            effects.append(Effect(EffectKind.COMPLETE_OBJECTIVE, oid))
        return Proposal(action_label=label.strip() or "contain", effects=effects, resolves_task=True, minutes=_COST["contain"])

    def _objectives_matching(self, event: TimelineEvent, keywords: tuple[str, ...]) -> list[int]:
        by_id = {o.id: o for o in self.engine.bundle.objectives}
        out: list[int] = []
        for oid in event.objective_ids:
            o = by_id.get(oid)
            if not o:
                continue
            if self.engine.state.objective_status.get(oid) == "Achieved":
                continue
            hay = f"{o.name} {o.description} {o.success_criteria}".lower()
            if any(k in hay for k in keywords):
                out.append(oid)
        return out

    def _recon_fact(self, event: TimelineEvent) -> str:
        # If this is the ticket that owns the containment fuse, triage exposes the
        # urgency — this is how a player learns which ticket is the one that bites.
        if self.engine.containment_deadline is not None and self.engine.flags_owned_by(event):
            mechanics = self.engine.bundle.scenario.game_mechanics
            warning = mechanics.deadline_warning if mechanics else (
                "This is the one that bites: it will detonate at the {deadline}m "
                "mark if you don't contain it. Handle this ticket first."
            )
            return warning.format(deadline=self.engine.containment_deadline)
        actors = self.engine.bundle.scenario.scenario_parameters
        if actors and actors.threat_actors:
            a = actors.threat_actors[0]
            ttps = ", ".join(a.ttps) if a.ttps else "unknown TTPs"
            return f"{a.name} ({a.type}) is the opposing actor — known for {ttps}."
        return "You triage the alert and confirm it is a real threat, not noise."
