"""Population segment models for aggregate response modeling."""

from pydantic import BaseModel, Field


class SegmentDemographics(BaseModel):
    urbanization: float = Field(0.5, ge=0.0, le=1.0)
    education_level: float = Field(0.5, ge=0.0, le=1.0, description="Normalized 0=none, 1=postgrad")
    median_age: float = Field(35.0, ge=0.0)
    economic_class: str = Field("middle", description="lower, middle, upper")


class SegmentDisposition(BaseModel):
    government_trust: float = Field(0.5, ge=0.0, le=1.0)
    nationalism: float = Field(0.5, ge=0.0, le=1.0)
    media_exposure: float = Field(0.5, ge=0.0, le=1.0)
    social_media_activity: float = Field(0.3, ge=0.0, le=1.0)
    protest_propensity: float = Field(0.2, ge=0.0, le=1.0)
    compliance_baseline: float = Field(0.7, ge=0.0, le=1.0)
    information_sources: list[str] = Field(
        default_factory=lambda: ["state_tv"],
        description="state_tv, social_media, foreign_media, word_of_mouth",
    )


class SegmentResponseParams(BaseModel):
    rally_around_flag_coefficient: float = Field(0.3, ge=0.0, le=1.0)
    economic_sensitivity: float = Field(0.5, ge=0.0, le=1.0)
    fatigue_rate: float = Field(0.1, ge=0.0, le=1.0)
    amplification_factor: float = Field(0.3, ge=0.0, le=1.0)


class PopulationSegment(BaseModel):
    name: str
    percentage: float = Field(ge=0.0, le=1.0, description="Fraction of total population")
    demographics: SegmentDemographics = Field(default_factory=SegmentDemographics)
    disposition: SegmentDisposition = Field(default_factory=SegmentDisposition)
    response_params: SegmentResponseParams = Field(default_factory=SegmentResponseParams)


class PopulationProfile(BaseModel):
    country: str
    total_population: int = 0
    period: str = Field("current", description="Historical period tag, e.g. 'current', 'world war 2'")
    segments: list[PopulationSegment] = Field(default_factory=list)

    def validate_segments(self) -> bool:
        total = sum(s.percentage for s in self.segments)
        return abs(total - 1.0) < 0.01
