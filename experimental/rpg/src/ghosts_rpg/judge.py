"""The adjudicator (Leitung) — a two-phase umpire.

Free-Kriegspiel judgment crossed with a matrix game:

  Phase A  assess(action, rationale) -> (OddsBand, critique)
           The judge weighs how plausible and doctrinally sound the player's move
           is against ground-truth world state, the mandate, and ROE. A strong
           rationale earns a better band. This is the *matrix-game* half — the
           player's reasoning literally moves the odds.

  Phase B  resolve(action, tier)     -> (narration, Deltas)
           Given the engine's hidden roll (an OutcomeTier), the judge narrates the
           consequence in free-Kriegspiel prose and proposes state deltas the engine
           validates. This is the *free-Kriegspiel* half — an umpire narrates a
           living world, not a spreadsheet.

LLM-driven when Ollama is configured. Offline it degrades to a keyword/length
heuristic band and templated narration — enough to run and test, not the full game.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

from .engine import Deltas, Engine
from .llm import make_llm
from .models import OddsBand, OutcomeTier


@dataclass
class Assessment:
    band: OddsBand
    critique: str


# Keywords that signal a sound, specific staff move (offline band heuristic only).
_SOUND = (
    "because", "since", "given", "isolate", "quarantine", "contain", "verify",
    "confirm", "coordinate", "attribute", "counter-message", "prebunk", "debunk",
    "escalate", "monitor", "correlate", "scope", "preserve", "notify", "brief",
)
# Keywords that signal a rash or implausible move.
_RASH = ("nuke", "hack back", "shut everything", "ignore", "panic", "guess", "hope")


class Judge:
    def __init__(self, engine: Engine, llm=None):
        self.engine = engine
        self.llm = llm or make_llm()

    # ── phase A: assess ─────────────────────────────────────────────────

    def assess(self, action: str, rationale: str) -> Assessment:
        return self._llm_assess(action, rationale) or self._offline_assess(action, rationale)

    def _offline_assess(self, action: str, rationale: str) -> Assessment:
        text = f"{action} {rationale}".lower().strip()
        if not text:
            return Assessment(OddsBand.LONGSHOT, "No action given; nothing to adjudicate.")
        if any(k in text for k in _RASH):
            return Assessment(
                OddsBand.LONGSHOT,
                "Umpire: the move is rash and outruns the picture you actually have.",
            )
        sound = sum(1 for k in _SOUND if k in text)
        has_rationale = len(rationale.strip()) >= 20
        if sound >= 2 and has_rationale:
            band = OddsBand.LIKELY
        elif sound >= 1 or has_rationale:
            band = OddsBand.EVEN
        else:
            band = OddsBand.UNLIKELY
        critique = (
            "Umpire: specific, reasoned tasking tied to the situation."
            if band is OddsBand.LIKELY
            else "Umpire: plausible, but the rationale is thin — tie it to what you know."
            if band is OddsBand.EVEN
            else "Umpire: vague order with little supporting reasoning; outcome is uncertain."
        )
        return Assessment(band, critique)

    def _llm_assess(self, action: str, rationale: str) -> Assessment | None:
        if not self.llm.enabled:
            return None
        raw = self.llm.generate(
            self._assess_prompt(action, rationale),
            system=(
                "You are Leitung, the umpire of a free-Kriegspiel staff exercise. "
                "Judge how plausible and doctrinally sound the player's move is "
                "against the world state, mandate, and rules of engagement. A "
                "specific, well-reasoned move earns better odds; a vague or rash one "
                "earns worse. Return ONLY JSON: "
                '{"band":"Likely|Even|Unlikely|Longshot","critique":"one sentence"}.'
            ),
        )
        data = _parse_object(raw)
        if not data:
            return None
        band = _coerce_band(data.get("band"))
        critique = str(data.get("critique", "")).strip()
        if band is None or not critique:
            return None
        return Assessment(band, critique)

    def _assess_prompt(self, action: str, rationale: str) -> str:
        sc = self.engine.scenario
        return json.dumps(
            {
                "scenario": {"name": sc.name, "situation": sc.situation},
                "player": {
                    "role": sc.player.role,
                    "mandate": sc.player.mandate,
                    "roe": sc.player.roe,
                },
                "objectives": [
                    {"id": o.id, "name": o.name, "successCriteria": o.success_criteria}
                    for o in sc.objectives
                ],
                "worldFacts": self.engine.state.facts,
                "indicatorsSeen": self.engine.state.indicators_seen[-8:],
                "clockMinutesLeft": self.engine.minutes_left,
                "playerMove": {"action": action, "rationale": rationale},
            },
            ensure_ascii=True,
            indent=2,
        )

    # ── phase B: resolve ────────────────────────────────────────────────

    def resolve(self, action: str, tier: OutcomeTier) -> tuple[str, Deltas]:
        result = self._llm_resolve(action, tier)
        if result is not None:
            return result
        return self._offline_resolve(action, tier)

    def _offline_resolve(self, action: str, tier: OutcomeTier) -> tuple[str, Deltas]:
        minutes = self.engine.tick_minutes
        # Objectives whose criteria this action plausibly satisfies (keyword match),
        # completed only on a clean success.
        matched = self._objectives_matching(action)
        if tier is OutcomeTier.SUCCESS:
            narration = f"Your order — {action} — lands cleanly. {self._effect_line(tier)}"
            # A clean success also sets the positive flags a matched objective is
            # gated on, so metWhen-driven objectives can actually close offline.
            flags = self._metwhen_flags(matched)
            return narration, Deltas(
                set_flags=flags, complete_objectives=matched, minutes=minutes
            )
        if tier is OutcomeTier.PARTIAL:
            narration = f"Your order — {action} — half-works. {self._effect_line(tier)}"
            return narration, Deltas(minutes=minutes)
        if tier is OutcomeTier.FAILURE:
            narration = f"Your order — {action} — does not take. {self._effect_line(tier)}"
            return narration, Deltas(minutes=minutes)
        # Backfire: the move helps the adversary.
        narration = f"Your order — {action} — backfires. {self._effect_line(tier)}"
        return narration, Deltas(opfor_progress=1, minutes=minutes)

    def _llm_resolve(self, action: str, tier: OutcomeTier) -> tuple[str, Deltas] | None:
        if not self.llm.enabled:
            return None
        raw = self.llm.generate(
            self._resolve_prompt(action, tier),
            system=(
                "You are Leitung narrating the consequence of the player's move in a "
                "free-Kriegspiel staff exercise. The outcome tier is fixed by the "
                "umpire's roll — narrate a realistic consequence consistent with it, "
                "in second person, 2-4 sentences, no dice or numbers. Then propose "
                "state deltas for the DEFENDER only. Use ONLY the objective ids and "
                "flag names listed (these are the defender's own flags; you do not "
                "control the adversary). Return ONLY JSON: "
                '{"narration":"...","setFlags":["..."],'
                '"completeObjectiveIds":[1],"failObjectiveIds":[]}.'
            ),
        )
        data = _parse_object(raw)
        if not data:
            return None
        narration = str(data.get("narration", "")).strip()
        if not narration:
            return None
        # The judge adjudicates the PLAYER's move: it may only set defender flags,
        # never OPFOR's advancement flags (the adversary earns those through its own
        # gated playbook). And it may only advance OPFOR as a modest consequence of a
        # genuine backfire — +1, never more — so no single ruling can hand the
        # adversary the game.
        allowed_flags = self.engine.defender_flags
        allowed_objs = set(self.engine.state.objective_status)
        opfor_gain = 1 if tier is OutcomeTier.BACKFIRE else 0
        deltas = Deltas(
            set_flags=[f for f in _strings(data.get("setFlags")) if f in allowed_flags],
            complete_objectives=[
                o for o in _ints(data.get("completeObjectiveIds")) if o in allowed_objs
            ],
            fail_objectives=[
                o for o in _ints(data.get("failObjectiveIds")) if o in allowed_objs
            ],
            opfor_progress=opfor_gain,
            minutes=self.engine.tick_minutes,
        )
        return narration, deltas

    def _resolve_prompt(self, action: str, tier: OutcomeTier) -> str:
        sc = self.engine.scenario
        return json.dumps(
            {
                "scenario": {"name": sc.name, "situation": sc.situation},
                "player": {"role": sc.player.role, "mandate": sc.player.mandate},
                "objectives": [
                    {"id": o.id, "name": o.name, "successCriteria": o.success_criteria}
                    for o in sc.objectives
                ],
                "flagsAllowed": sorted(self.engine.defender_flags),
                "worldFacts": self.engine.state.facts,
                "playerAction": action,
                "outcomeTier": tier.value,
            },
            ensure_ascii=True,
            indent=2,
        )

    # ── offline helpers ─────────────────────────────────────────────────

    def _effect_line(self, tier: OutcomeTier) -> str:
        return {
            OutcomeTier.SUCCESS: "Exercise control records the effect you intended.",
            OutcomeTier.PARTIAL: "Some of the effect holds; the rest slips.",
            OutcomeTier.FAILURE: "The situation is unchanged and the clock keeps running.",
            OutcomeTier.BACKFIRE: "Worse: the adversary gains from the misstep.",
        }[tier]

    def _metwhen_flags(self, objective_ids: list[int]) -> list[str]:
        """The positive flags (flag:x terms) an objective's metWhen gate needs, so a
        clean-success offline resolution can satisfy it. Only flags the scenario
        tracks are proposed; the engine validates again."""
        by_id = {o.id: o for o in self.engine.scenario.objectives}
        flags: list[str] = []
        for oid in objective_ids:
            obj = by_id.get(oid)
            if not obj or not obj.met_when:
                continue
            for raw in obj.met_when.split("&&"):
                term = raw.strip()
                if term.startswith("flag:"):
                    flag = term[len("flag:"):].strip()
                    if flag and flag in self.engine.known_flags and flag not in flags:
                        flags.append(flag)
        return flags

    def _objectives_matching(self, action: str) -> list[int]:
        text = action.lower()
        out: list[int] = []
        for o in self.engine.scenario.objectives:
            if self.engine.state.objective_status.get(o.id) == "Achieved":
                continue
            hay = f"{o.name} {o.description} {o.success_criteria}".lower()
            words = [w for w in hay.split() if len(w) >= 5]
            if any(w in text for w in words):
                out.append(o.id)
        return out


# ── module-level JSON helpers ───────────────────────────────────────────


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


def _coerce_band(value) -> OddsBand | None:
    try:
        return OddsBand(str(value).strip().title())
    except ValueError:
        return None


def _strings(value) -> list[str]:
    items = value if isinstance(value, list) else [value] if isinstance(value, str) else []
    return [s for s in (str(i).strip() for i in items) if s]


def _ints(value) -> list[int]:
    items = value if isinstance(value, list) else [value] if isinstance(value, int) else []
    out: list[int] = []
    for i in items:
        try:
            out.append(int(i))
        except (TypeError, ValueError):
            continue
    return out
