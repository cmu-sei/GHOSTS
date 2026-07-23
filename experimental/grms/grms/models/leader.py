"""Leader profile and decision context models."""

from datetime import datetime
from uuid import UUID, uuid4

from pydantic import BaseModel, Field


class LeaderPersonality(BaseModel):
    risk_tolerance: float = Field(0.0, ge=-1.0, le=1.0, description="cautious (-1) to reckless (+1)")
    authoritarianism: float = Field(0.0, ge=-1.0, le=1.0, description="democratic (-1) to autocratic (+1)")
    nationalism: float = Field(0.0, ge=-1.0, le=1.0, description="internationalist (-1) to nationalist (+1)")
    pragmatism: float = Field(0.0, ge=-1.0, le=1.0, description="ideological (-1) to pragmatic (+1)")
    aggression: float = Field(0.0, ge=-1.0, le=1.0, description="dovish (-1) to hawkish (+1)")
    populism: float = Field(0.0, ge=-1.0, le=1.0, description="elitist (-1) to populist (+1)")
    transparency: float = Field(0.0, ge=-1.0, le=1.0, description="secretive (-1) to transparent (+1)")
    religiosity: float = Field(0.0, ge=-1.0, le=1.0, description="secular (-1) to theocratic (+1)")


class LeaderIdeology(BaseModel):
    economic_system: str = Field("mixed", description="free_market, state_directed, mixed")
    geopolitical_orientation: str = Field("non_aligned", description="western, non_aligned, eastern")
    key_alliances: list[str] = Field(default_factory=list)
    adversaries: list[str] = Field(default_factory=list)
    red_lines: list[str] = Field(default_factory=list, description="Triggers for maximum response")
    core_narratives: list[str] = Field(default_factory=list, description="Recurring rhetorical themes")


class HistoricalDecision(BaseModel):
    event_description: str
    decision_taken: str
    outcome: str
    date: datetime
    context_factors: list[str] = Field(default_factory=list)


class LeaderCulturalContext(BaseModel):
    decision_making_style: str = Field("advisory_council", description="consensus, unilateral, advisory_council")
    information_sources: list[str] = Field(default_factory=list)
    domestic_power_base: str = ""
    succession_concerns: bool = False
    historical_analogies: list[str] = Field(default_factory=list, description="Events this leader references")


class EconomicConditions(BaseModel):
    gdp_growth: float = 0.0
    unemployment: float = 0.0
    inflation: float = 0.0
    sanctions_pressure: float = Field(0.0, ge=0.0, le=1.0)


class MilitaryPosture(BaseModel):
    readiness_level: str = Field("peacetime", description="peacetime, elevated, mobilized")
    ongoing_operations: list[str] = Field(default_factory=list)
    force_disposition: str = ""


class DomesticPressures(BaseModel):
    election_proximity_days: int | None = None
    opposition_strength: float = Field(0.3, ge=0.0, le=1.0)
    media_sentiment: float = Field(0.0, ge=-1.0, le=1.0)
    protest_level: str = Field("none", description="none, minor, major, crisis")


class LeaderDecisionContext(BaseModel):
    approval_rating: float = Field(50.0, ge=0.0, le=100.0)
    economic_conditions: EconomicConditions = Field(default_factory=EconomicConditions)
    military_posture: MilitaryPosture = Field(default_factory=MilitaryPosture)
    domestic_pressures: DomesticPressures = Field(default_factory=DomesticPressures)
    ongoing_conflicts: list[str] = Field(default_factory=list)
    recent_events: list[str] = Field(default_factory=list, description="Last N events in this scenario")


class LeaderProfile(BaseModel):
    id: UUID = Field(default_factory=uuid4)
    name: str
    country: str
    title: str = ""
    period: str = Field("current", description="Historical period tag, e.g. 'current', 'world war 2'")
    personality: LeaderPersonality = Field(default_factory=LeaderPersonality)
    ideology: LeaderIdeology = Field(default_factory=LeaderIdeology)
    decision_history: list[HistoricalDecision] = Field(default_factory=list)
    cultural_context: LeaderCulturalContext = Field(default_factory=LeaderCulturalContext)
    decision_context: LeaderDecisionContext = Field(default_factory=LeaderDecisionContext)
