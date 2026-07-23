"""Models for the cognitive decision engine — explainable parametric predictions."""

from pydantic import BaseModel, Field


class LayerContribution(BaseModel):
    layer: str = Field(description="identity, personality, belief, motivation, context, history")
    weight: float = Field(ge=0.0, le=1.0)
    raw_score: float = Field(ge=-1.0, le=1.0)
    weighted_contribution: float
    rationale: str = Field(default="", description="Human-readable explanation of why this layer scored this way")


class InteractionEffect(BaseModel):
    description: str = Field(description="e.g. 'personality.aggression × belief.hostile_intent'")
    weight: float
    magnitude: float
    effect_on_selected: float


class ActionScore(BaseModel):
    action: str
    final_score: float
    layer_contributions: list[LayerContribution]
    interaction_effects: list[float] = Field(default_factory=list)
    rank: int = 0


class CognitiveDecisionResult(BaseModel):
    selected_action: str
    confidence: float = Field(ge=0.0, le=1.0, description="Gap between top and runner-up normalized")
    escalation_estimate: float = Field(ge=0.0, le=1.0, description="Derived from selected action's escalation affordance")
    action_scores: list[ActionScore] = Field(description="All actions ranked by score")
    interaction_effects: list[InteractionEffect] = Field(default_factory=list)
    formula_version: str = "geopolitical_red_line_response_v1"
