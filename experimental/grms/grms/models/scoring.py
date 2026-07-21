"""Models for batch scoring predictions against known historical outcomes."""

from datetime import datetime
from uuid import UUID, uuid4

from pydantic import BaseModel, Field

from grms.models.events import GeopoliticalEvent
from grms.models.leader import LeaderProfile


class KnownOutcome(BaseModel):
    escalation_risk: float | None = Field(None, ge=0.0, le=1.0)
    action_types: list[str] = Field(default_factory=list)
    tone: str | None = None
    description: str = Field("", description="What actually happened historically")


class PredictionScore(BaseModel):
    """Inline scoring result for a prediction that has a known outcome."""
    overall_score: float = Field(ge=0.0, le=1.0)
    escalation_score: float | None = Field(None, ge=0.0, le=1.0)
    action_type_score: float | None = Field(None, ge=0.0, le=1.0)
    tone_score: float | None = Field(None, ge=0.0, le=1.0)
    dimensions: list["ScoreDimension"] = Field(default_factory=list)
    scored_at: datetime = Field(default_factory=datetime.utcnow)


class ScoringCase(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    label: str = Field(description="e.g. 'Fall of France 1940'")
    historical_date: str | None = Field(None, description="ISO date of the historical event, e.g. '1940-05-10'")
    era: str = Field("ww2", description="Scenario era: ww2, cold_war, post_911, modern")
    leader: LeaderProfile
    event: GeopoliticalEvent
    known_outcome: KnownOutcome


class RunMetadata(BaseModel):
    llm_source: str
    llm_model: str
    llm_temperature: float
    llm_seed: int | None = None
    prompt_hash: str = Field(description="SHA-256 of system+event prompt templates")
    scenario_era: str = "all"
    git_commit: str | None = None
    notes: str = ""


class ScoreDimension(BaseModel):
    dimension: str
    predicted: str | float | list
    actual: str | float | list
    score: float = Field(ge=0.0, le=1.0)
    note: str = ""


class CaseResult(BaseModel):
    case_id: str
    label: str
    overall_score: float = Field(ge=0.0, le=1.0)
    dimensions: list[ScoreDimension]
    predicted_escalation_risk: float
    predicted_actions: list[str]
    predicted_tone: str
    confidence: float


class BatchRunResult(BaseModel):
    id: UUID = Field(default_factory=uuid4)
    run_at: datetime = Field(default_factory=datetime.utcnow)
    llm_model: str
    total_cases: int
    aggregate_score: float = Field(ge=0.0, le=1.0)
    escalation_rmse: float
    escalation_bias: float = Field(description="Positive = over-predicts, negative = under-predicts")
    action_type_accuracy: float = Field(ge=0.0, le=1.0)
    tone_accuracy: float = Field(ge=0.0, le=1.0)
    cases: list[CaseResult]
    failures: list[str] = Field(default_factory=list)
    metadata: RunMetadata | None = None
    is_regression: bool = False


class CalibrationBin(BaseModel):
    bin_lower: float
    bin_upper: float
    mean_predicted: float
    mean_actual: float
    count: int


class BrierDecomposition(BaseModel):
    brier_score: float = Field(description="Mean squared error of probabilistic predictions (lower is better)")
    reliability: float = Field(description="Calibration penalty — how far bin means deviate from diagonal")
    resolution: float = Field(description="Discrimination ability — spread of bin means vs base rate (higher is better)")
    uncertainty: float = Field(description="Base rate entropy — irreducible given the outcome distribution")
    n_cases: int = 0


class ConfCalBin(BaseModel):
    confidence_lower: float
    confidence_upper: float
    mean_confidence: float
    fraction_correct: float = Field(description="Fraction of predictions in this bin that were above-median accuracy")
    count: int


class ConfidenceCalibration(BaseModel):
    bins: list[ConfCalBin] = Field(default_factory=list)
    n_cases: int = 0
    note: str = ""


class AblationCaseResult(BaseModel):
    case_id: str
    label: str
    full_model_score: float
    baseline_scores: dict[str, float] = Field(description="baseline_name -> score")
    value_added: float = Field(description="full_model_score - best_baseline_score")


class AblationResult(BaseModel):
    full_model_aggregate: float
    baseline_aggregates: dict[str, float] = Field(description="baseline_name -> aggregate score")
    value_added: float = Field(description="full_model_aggregate - best_baseline_aggregate")
    best_baseline: str
    cases: list[AblationCaseResult] = Field(default_factory=list)
    total_cases: int = 0
    baselines_used: list[str] = Field(default_factory=list)


class EraBreakdown(BaseModel):
    era: str
    case_count: int
    aggregate_score: float
    escalation_rmse: float
    action_type_accuracy: float
    tone_accuracy: float


class LeaderBreakdown(BaseModel):
    leader_name: str
    country: str
    case_count: int
    aggregate_score: float


class ConfidenceBand(BaseModel):
    metric: str
    point_estimate: float
    ci_lower: float
    ci_upper: float
    confidence_level: float = 0.95


class StakeholderReport(BaseModel):
    generated_at: datetime = Field(default_factory=datetime.utcnow)
    total_runs: int
    total_scenarios_evaluated: int
    eras_covered: list[str]

    latest_aggregate_score: float
    latest_escalation_rmse: float
    latest_action_accuracy: float
    latest_tone_accuracy: float

    confidence_bands: list[ConfidenceBand] = Field(
        default_factory=list,
        description="95% CIs for key metrics based on historical run variance",
    )

    forward_confidence: float = Field(
        description="Expected accuracy for future predictions based on backtest performance"
    )
    forward_confidence_note: str = Field(
        default="",
        description="Explanation of how forward confidence is derived",
    )

    trend_timestamps: list[str] = Field(default_factory=list)
    trend_aggregate_scores: list[float] = Field(default_factory=list)
    trend_escalation_rmse: list[float] = Field(default_factory=list)

    by_era: list[EraBreakdown] = Field(default_factory=list)
    by_leader: list[LeaderBreakdown] = Field(default_factory=list)
    by_event_type: dict[str, float] = Field(default_factory=dict)

    calibration_bins: list[CalibrationBin] = Field(default_factory=list)

    regression_detected: bool = False
    regression_details: str | None = None

    current_model: str = ""
    current_prompt_hash: str = ""
    runs_with_same_config: int = 0
