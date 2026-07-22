"""Domain models for the RPG.

Two layers:

1. The *scenario* model — a faithful, read-only in-memory mirror of what the GHOSTS
   API returns from the three GETs the loader consumes (scenario, graph, objectives).
   Field aliases mirror the live camelCase JSON so the same model parses a live API
   response or a bundled fixture export interchangeably.

2. The *game* model — mutable runtime state the engine owns (step pointer, flags,
   objective statuses, knowledge/inventory, transcript). The DM never mutates this
   directly; it only proposes effects the engine validates and applies.
"""

from __future__ import annotations

from enum import Enum
from typing import Optional

from pydantic import BaseModel, ConfigDict, Field


# ──────────────────────────────────────────────
# Shared config: parse camelCase JSON, allow access by python name too.
# ──────────────────────────────────────────────


class _ApiModel(BaseModel):
    model_config = ConfigDict(populate_by_name=True, extra="ignore")


# ──────────────────────────────────────────────
# The flip-flop: who owns a timeline event.
# ──────────────────────────────────────────────


class Cell(str, Enum):
    """The `Assigned` field on a timeline event. Blue Team = the player's turn;
    every other cell is played by the computer (the Dungeon Master)."""

    BLUE = "Blue Team"
    RED = "Red Team"
    WHITE = "White Cell"
    GREEN = "Green Cell"


# ──────────────────────────────────────────────
# Scenario model (read-only mirror of the GHOSTS API)
# ──────────────────────────────────────────────


class TimelineEvent(_ApiModel):
    number: int
    time: str = ""
    assigned: str = ""
    description: str = ""
    status: str = "Pending"
    objective_ids: list[int] = Field(default_factory=list, alias="objectiveIds")
    trigger_kind: str = Field(default="PointInTime", alias="triggerKind")
    schedule: Optional[str] = None
    trigger_condition: Optional[str] = Field(default=None, alias="triggerCondition")
    execution_type: str = Field(default="manual", alias="executionType")
    workflow_id: Optional[str] = Field(default=None, alias="workflowId")

    @property
    def cell(self) -> Optional[Cell]:
        """Parsed `Assigned` cell, or None if it is an unrecognized value."""
        try:
            return Cell(self.assigned)
        except ValueError:
            return None

    @property
    def is_player_turn(self) -> bool:
        return self.cell is Cell.BLUE


class Timeline(_ApiModel):
    exercise_duration: int = Field(default=0, alias="exerciseDuration")
    events: list[TimelineEvent] = Field(default_factory=list)


class Nation(_ApiModel):
    name: str = ""
    alignment: str = ""


class ThreatActor(_ApiModel):
    name: str = ""
    type: str = ""
    capability: int = 0
    ttps: list[str] = Field(default_factory=list)


class Inject(_ApiModel):
    trigger: str = ""
    title: str = ""


class UserPool(_ApiModel):
    role: str = ""
    count: int = 0


class ScenarioParameters(_ApiModel):
    nations: list[Nation] = Field(default_factory=list)
    threat_actors: list[ThreatActor] = Field(default_factory=list, alias="threatActors")
    injects: list[Inject] = Field(default_factory=list)
    user_pools: list[UserPool] = Field(default_factory=list, alias="userPools")
    player_role: str = Field(default="Blue Team (SOC Analyst)", alias="playerRole")
    objectives: str = ""
    political_context: str = Field(default="", alias="politicalContext")
    rules_of_engagement: str = Field(default="", alias="rulesOfEngagement")
    victory_conditions: str = Field(default="", alias="victoryConditions")


class GameMechanics(_ApiModel):
    """The subset of scenario game mechanics the RPG uses to pace the day.

    `duration_hours` sets the lunch clock: the player is trying to clear the
    worklist before the exercise window (their morning) runs out."""

    timeline_type: str = Field(default="turn-based", alias="timelineType")
    duration_hours: float = Field(default=1.0, alias="durationHours")
    # The fuse: minutes on the lunch clock after which an un-contained threat
    # detonates and forces the loss branch. None => no deadline (threat waits).
    containment_deadline_minutes: Optional[int] = Field(
        default=None, alias="containmentDeadlineMinutes"
    )
    window_label: str = Field(default="to lunch", alias="windowLabel")
    deadline_label: str = Field(default="CONTAINMENT FUSE", alias="deadlineLabel")
    decisive_action_label: str = Field(
        default="issue containment order", alias="decisiveActionLabel"
    )
    deadline_warning: str = Field(
        default=(
            "This is the one that bites: it will detonate at the {deadline}m mark "
            "if you don't contain it. Handle this ticket first."
        ),
        alias="deadlineWarning",
    )
    deadline_failure_message: str = Field(
        default="The clock ran out on the threat — it detonated before you contained it.",
        alias="deadlineFailureMessage",
    )


class Vulnerability(_ApiModel):
    asset: str = ""
    cve: str = ""
    severity: str = ""


class TechnicalEnvironment(_ApiModel):
    network_topology: str = Field(default="", alias="networkTopology")
    services: str = ""
    assets: str = ""
    defenses: list[str] = Field(default_factory=list)
    vulnerabilities: list[Vulnerability] = Field(default_factory=list)


class Entity(_ApiModel):
    id: str
    name: str = ""
    entity_type: str = Field(default="Custom", alias="entityType")
    description: str = ""
    properties: str = "{}"
    external_id: str = Field(default="", alias="externalId")


class Edge(_ApiModel):
    id: str
    source_entity_id: str = Field(alias="sourceEntityId")
    target_entity_id: str = Field(alias="targetEntityId")
    edge_type: str = Field(default="Custom", alias="edgeType")
    label: str = ""


class Graph(_ApiModel):
    nodes: list[Entity] = Field(default_factory=list)
    edges: list[Edge] = Field(default_factory=list)


class Objective(_ApiModel):
    id: int
    scenario_id: Optional[int] = Field(default=None, alias="scenarioId")
    name: str = ""
    description: str = ""
    type: str = "MET"
    status: str = "Draft"
    score: str = "U"
    priority: int = 1
    success_criteria: str = Field(default="", alias="successCriteria")
    assigned: str = ""
    sort_order: int = Field(default=0, alias="sortOrder")


class Scenario(_ApiModel):
    id: int
    name: str = ""
    description: str = ""
    builder_status: str = Field(default="None", alias="builderStatus")
    scenario_parameters: Optional[ScenarioParameters] = Field(
        default=None, alias="scenarioParameters"
    )
    technical_environment: Optional[TechnicalEnvironment] = Field(
        default=None, alias="technicalEnvironment"
    )
    game_mechanics: Optional[GameMechanics] = Field(default=None, alias="gameMechanics")
    timeline: Optional[Timeline] = None


class ScenarioCatalog(_ApiModel):
    listed: bool = False
    sort_order: int = Field(default=0, alias="sortOrder")
    era: str = ""
    theater: str = ""
    estimated_minutes: int = Field(default=0, alias="estimatedMinutes")


class ScenarioBundle(BaseModel):
    """The three GETs the loader consumes, assembled into one object.

    A fixture export is exactly this shape on disk; a live load assembles it from
    three separate API calls. Everything downstream depends only on this."""

    model_config = ConfigDict(populate_by_name=True, extra="ignore")

    scenario: Scenario
    graph: Graph = Field(default_factory=Graph)
    objectives: list[Objective] = Field(default_factory=list)
    catalog: Optional[ScenarioCatalog] = None


# ──────────────────────────────────────────────
# Game model (mutable runtime state owned by the engine)
# ──────────────────────────────────────────────


class TranscriptEntry(BaseModel):
    """One resolved turn of the game — canonical, engine-owned record."""

    step_number: int
    cell: str
    speaker: str  # "DM" or "Player"
    text: str


class StaffProduct(BaseModel):
    """A Blue-Team staff product submitted to exercise control.

    Short commands still work, but a richer submission can use staff-style
    sections such as Priority, Plan, Assumptions, Information Requests, and Risk.
    The controller parses these fields and adjudicates them against scenario
    constraints before the engine applies effects.
    """

    raw_text: str
    priority: str = ""
    intent: str = ""
    plan: str = ""
    assumptions: list[str] = Field(default_factory=list)
    information_requests: list[str] = Field(default_factory=list)
    risk_acceptance: str = ""
    is_structured: bool = False


class GameState(BaseModel):
    """Mutable runtime state. The engine is the only writer."""

    current_step: int = 0  # Number of the current/next event; 0 = not started
    flags: set[str] = Field(default_factory=set)
    objective_status: dict[int, str] = Field(default_factory=dict)
    knowledge: list[str] = Field(default_factory=list)
    inventory: list[str] = Field(default_factory=list)
    transcript: list[TranscriptEntry] = Field(default_factory=list)
    umpire_findings: list[str] = Field(default_factory=list)
    assumptions: list[str] = Field(default_factory=list)
    is_complete: bool = False

    # Worklist + lunch clock. The player clears open Blue-Team tasks in any order;
    # each action burns minutes off the exercise window. cleared_steps holds the
    # Numbers of Blue-Team events already resolved in the current worklist.
    cleared_steps: set[int] = Field(default_factory=set)
    minutes_spent: int = 0
    # Which open Blue-Team tasks are currently *surfaced* to the player. Tickets
    # arrive one at a time: only the first open task is revealed by default;
    # tabling a ticket reveals the next while keeping the current one open, and
    # resolving one auto-reveals the next. Reset each worklist.
    revealed_steps: set[int] = Field(default_factory=set)
    # Set once the containment fuse blows (deadline passed with the steering flag
    # unset): the threat detonated, the loss branch is locked, late containment is
    # refused.
    detonated: bool = False
