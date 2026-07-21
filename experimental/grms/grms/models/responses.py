"""Response models for leader and population predictions."""

from datetime import datetime
from typing import Any
from uuid import UUID

from pydantic import BaseModel, Field


class VerbalResponse(BaseModel):
    statement: str
    tone: str = Field(description="defiant, measured, conciliatory, threatening, dismissive")
    audience: str = Field(description="domestic, international, adversary, military")


class LeaderAction(BaseModel):
    action_type: str = Field(description="military, diplomatic, economic, information, internal")
    description: str
    timeline: str = Field(description="immediate, hours, days, weeks")
    likelihood: float = Field(ge=0.0, le=1.0)
    reversibility: str = Field("reversible")


class ResponseReasoning(BaseModel):
    personality_factors: list[str] = Field(default_factory=list, description="Which profile dimensions drove this response")
    historical_precedents: list[str] = Field(default_factory=list, description="Which decision_history entries are analogous")
    contextual_drivers: list[str] = Field(default_factory=list, description="Which decision_context factors matter")
    constraints: list[str] = Field(default_factory=list, description="What limits the response options")
    confidence: float = Field(ge=0.0, le=1.0)


class LeaderResponse(BaseModel):
    event_id: UUID
    leader_id: UUID
    timestamp: datetime = Field(default_factory=datetime.utcnow)
    verbal_response: VerbalResponse
    actions: list[LeaderAction]
    reasoning: ResponseReasoning
    escalation_risk: float = Field(ge=0.0, le=1.0)
    de_escalation_openings: list[str] = Field(default_factory=list)
    cognitive_trace: Any | None = Field(
        default=None, description="CognitiveDecisionResult trace if hybrid mode was used"
    )
    hybrid_divergence: float | None = Field(
        default=None, description="Divergence between LLM and cognitive engine (0=agree, 1=fully disagree)"
    )


class PopulationOverall(BaseModel):
    support_leader: float = Field(ge=0.0, le=1.0)
    oppose_leader: float = Field(ge=0.0, le=1.0)
    neutral: float = Field(ge=0.0, le=1.0)
    protest_likelihood: float = Field(ge=0.0, le=1.0)
    compliance_likelihood: float = Field(ge=0.0, le=1.0)


class SegmentResponse(BaseModel):
    segment_name: str
    support: float = Field(ge=0.0, le=1.0)
    oppose: float = Field(ge=0.0, le=1.0)
    neutral: float = Field(ge=0.0, le=1.0)
    amplification: float = Field(ge=0.0, le=1.0)
    protest_propensity: float = Field(ge=0.0, le=1.0)
    key_concerns: list[str] = Field(default_factory=list)


class InformationSpread(BaseModel):
    viral_probability: float = Field(ge=0.0, le=1.0)
    dominant_narrative: str
    counter_narratives: list[str] = Field(default_factory=list)
    time_to_saturation_hours: float = Field(ge=0.0)


class PopulationTrajectory(BaseModel):
    support_7d: float = Field(ge=0.0, le=1.0)
    support_30d: float = Field(ge=0.0, le=1.0)
    fatigue_onset_days: int = Field(ge=0)


class PopulationResponse(BaseModel):
    event_id: UUID
    country: str
    timestamp: datetime = Field(default_factory=datetime.utcnow)
    overall: PopulationOverall
    segment_responses: list[SegmentResponse]
    information_spread: InformationSpread
    trajectory: PopulationTrajectory


class CombinedResponse(BaseModel):
    leader: LeaderResponse
    population: PopulationResponse
