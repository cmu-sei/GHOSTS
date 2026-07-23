"""Geopolitical event models."""

from datetime import datetime
from uuid import UUID, uuid4

from pydantic import BaseModel, Field


class EventStructured(BaseModel):
    actor: str = Field(description="Country or entity performing the action")
    target: str = Field(description="Country or entity affected")
    action_category: str = Field(description="military, diplomatic, economic, cyber, information")
    reversibility: str = Field("reversible", description="irreversible, escalatory, reversible")
    visibility: str = Field("public", description="public, leaked, covert")


class GeopoliticalEvent(BaseModel):
    id: UUID = Field(default_factory=uuid4)
    timestamp: datetime = Field(default_factory=datetime.utcnow)
    event_type: str = Field(description="military_action, diplomatic, economic, cyber, information")
    severity: float = Field(ge=0.0, le=1.0)
    description: str
    structured: EventStructured
    scenario_id: str | None = None
    execution_id: int | None = None
    preceding_events: list[UUID] = Field(default_factory=list)
