"""Domain models for Kriegspiel Mode.

Two layers:

1. The *scenario* model — a read-only authored spec (situation, player mandate,
   OPFOR objective + playbook, mutable world facts, scheduled triggers, clock, and
   fog policy). The author supplies the *pieces* an exercise is made of, not a fixed
   sequence of events: the loop composes what actually happens at runtime.

2. The *world* model — mutable runtime state the engine owns (facts, flags, the
   clock, objective statuses, OPFOR progress, what the player has perceived, and the
   transcript). The judge and OPFOR agents only *propose* deltas; the engine
   validates and applies them.

The old timeline-walker models (Timeline/TimelineEvent/GameMechanics/...) are gone:
this loop has no fixed event list. See the un-loaded fixtures under fixtures/ for the
prior worklist format.
"""

from __future__ import annotations

from enum import Enum
from typing import Optional

from pydantic import BaseModel, ConfigDict, Field


class _ApiModel(BaseModel):
    """Parse camelCase JSON, allow access by python name, ignore unknown keys."""

    model_config = ConfigDict(populate_by_name=True, extra="ignore")


# ──────────────────────────────────────────────
# Adjudication vocabulary
# ──────────────────────────────────────────────


class OddsBand(str, Enum):
    """How likely the judge thinks the player's move is to work, given their
    rationale against world state and doctrine. The matrix-game half: a strong,
    plausible argument earns a better band."""

    LIKELY = "Likely"
    EVEN = "Even"
    UNLIKELY = "Unlikely"
    LONGSHOT = "Longshot"


class OutcomeTier(str, Enum):
    """What the hidden roll returns once resolved against the band."""

    SUCCESS = "Success"
    PARTIAL = "Partial"
    FAILURE = "Failure"
    BACKFIRE = "Backfire"


# ──────────────────────────────────────────────
# Scenario spec (read-only, authored)
# ──────────────────────────────────────────────


class PlayerSpec(_ApiModel):
    role: str = ""
    mandate: str = ""
    roe: str = ""  # rules of engagement / constraints the judge holds the player to


class ScenarioObjective(_ApiModel):
    """A player objective, scored met/failed against authored criteria."""

    id: int
    name: str = ""
    description: str = ""
    success_criteria: str = Field(default="", alias="successCriteria")
    # Optional gate: an objective auto-marks Achieved when this condition holds
    # (same grammar as triggers). Empty => only the judge can complete it.
    met_when: str = Field(default="", alias="metWhen")
    priority: int = 1


class PlaybookMove(_ApiModel):
    """One thing OPFOR *could* do. The set of moves whose preconditions currently
    hold is OPFOR's live menu; the OPFOR agent picks one each turn."""

    id: str
    domain: str = "cyber"  # cyber | cognitive | hybrid
    description: str = ""
    # Preconditions on world flags, same grammar as triggers (flag:x / !x /
    # objective:N / clock>=N joined by &&). Empty => always available.
    preconds: str = ""
    # Deltas this move applies to ground-truth world state when OPFOR plays it.
    set_flags: list[str] = Field(default_factory=list, alias="setFlags")
    set_facts: dict[str, str] = Field(default_factory=dict, alias="setFacts")
    # Advances OPFOR toward its objective by this many points (win threshold below).
    progress: int = 0
    # Observable signatures this move emits. The fog filter decides which the player
    # actually perceives.
    indicators: list[str] = Field(default_factory=list)


class OpforSpec(_ApiModel):
    name: str = ""
    objective: str = ""
    # OPFOR wins when accumulated move progress reaches this. 0 => never (player can
    # only win/lose on their own objectives + the clock).
    win_threshold: int = Field(default=0, alias="winThreshold")
    playbook: list[PlaybookMove] = Field(default_factory=list)


class Trigger(_ApiModel):
    """A scheduled/conditional inject — the clock's teeth. Fires once when `when`
    holds; applies its deltas to ground-truth state and emits indicators."""

    id: str
    when: str = ""  # condition grammar (flag:x / !x / objective:N / clock>=N && ...)
    inject: str = ""
    set_flags: list[str] = Field(default_factory=list, alias="setFlags")
    set_facts: dict[str, str] = Field(default_factory=dict, alias="setFacts")
    indicators: list[str] = Field(default_factory=list)


class ClockSpec(_ApiModel):
    window_minutes: int = Field(default=60, alias="windowMinutes")
    tick_minutes: int = Field(default=10, alias="tickMinutes")
    label: str = "exercise window"


class FogSpec(_ApiModel):
    # partial: player sees emitted indicators, never ground-truth flags/facts.
    # full: player sees nothing OPFOR does until an indicator surfaces it (strict).
    # off: player sees ground truth (training/debug).
    default: str = "partial"


class WorldSpec(_ApiModel):
    """Authored initial ground truth. `facts` and `flags` seed WorldState."""

    assets: list[str] = Field(default_factory=list)
    facts: dict[str, str] = Field(default_factory=dict)
    flags: list[str] = Field(default_factory=list)
    narrative_env: dict[str, str] = Field(default_factory=dict, alias="narrativeEnv")


class Scenario(_ApiModel):
    id: int = 0
    name: str = ""
    description: str = ""
    situation: str = ""  # the kickoff briefing shown at T+0
    player: PlayerSpec = Field(default_factory=PlayerSpec)
    opfor: OpforSpec = Field(default_factory=OpforSpec)
    world: WorldSpec = Field(default_factory=WorldSpec)
    triggers: list[Trigger] = Field(default_factory=list)
    clock: ClockSpec = Field(default_factory=ClockSpec)
    fog: FogSpec = Field(default_factory=FogSpec)
    objectives: list[ScenarioObjective] = Field(default_factory=list)


class ScenarioCatalog(_ApiModel):
    listed: bool = False
    sort_order: int = Field(default=0, alias="sortOrder")
    era: str = ""
    theater: str = ""
    estimated_minutes: int = Field(default=0, alias="estimatedMinutes")


class ScenarioBundle(BaseModel):
    """A loadable scenario. A fixture file is exactly this shape on disk."""

    model_config = ConfigDict(populate_by_name=True, extra="ignore")

    scenario: Scenario
    catalog: Optional[ScenarioCatalog] = None


# ──────────────────────────────────────────────
# World model (mutable runtime state owned by the engine)
# ──────────────────────────────────────────────


class TranscriptEntry(BaseModel):
    """One recorded beat of the game — canonical, engine-owned."""

    turn: int
    speaker: str  # "Judge" | "Player" | "Signals" | "Control"
    text: str


class TurnRecord(BaseModel):
    """The adjudication trace for one player turn — feeds the AAR debrief."""

    turn: int
    action: str
    rationale: str
    band: str
    tier: str
    critique: str = ""


class WorldState(BaseModel):
    """Mutable ground truth. The engine is the only writer."""

    turn: int = 0
    clock_minutes: int = 0
    flags: set[str] = Field(default_factory=set)
    facts: dict[str, str] = Field(default_factory=dict)
    objective_status: dict[int, str] = Field(default_factory=dict)

    # OPFOR progress toward win_threshold, and which playbook moves it has spent.
    opfor_progress: int = 0
    opfor_moves_played: list[str] = Field(default_factory=list)

    # Triggers already fired (by id) so each fires at most once.
    fired_triggers: set[str] = Field(default_factory=set)

    # What the player has perceived (fog-filtered) — their working picture.
    indicators_seen: list[str] = Field(default_factory=list)

    transcript: list[TranscriptEntry] = Field(default_factory=list)
    rulings: list[TurnRecord] = Field(default_factory=list)

    is_complete: bool = False
    outcome: str = ""  # "" until complete, then WIN | LOSS | INCOMPLETE
