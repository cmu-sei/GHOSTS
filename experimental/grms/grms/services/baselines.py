"""Baseline predictors for ablation testing.

Each baseline produces a LeaderResponse to enable direct comparison with the
full GRMS model. If the full model cannot beat these, leader profiles have not
been demonstrated to add predictive value.
"""

import json
import logging
from pathlib import Path
from uuid import uuid4

from jinja2 import Environment, FileSystemLoader

from grms.llm.base import get_provider
from grms.models.events import GeopoliticalEvent
from grms.models.leader import LeaderProfile
from grms.models.responses import (
    LeaderAction,
    LeaderResponse,
    ResponseReasoning,
    VerbalResponse,
)

logger = logging.getLogger("grms.baselines")

PROMPTS_DIR = Path(__file__).parent.parent / "prompts"
jinja_env = Environment(loader=FileSystemLoader(str(PROMPTS_DIR)))


class NullPredictor:
    """Always predicts base-rate values. The minimum bar any model must beat."""

    MEAN_ESCALATION = 0.55
    DEFAULT_ACTIONS = ["diplomatic", "military"]
    DEFAULT_TONE = "measured"

    async def predict(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        return LeaderResponse(
            event_id=event.id,
            leader_id=leader.id,
            verbal_response=VerbalResponse(
                statement="The government is monitoring the situation.",
                tone=self.DEFAULT_TONE,
                audience="domestic",
            ),
            actions=[
                LeaderAction(
                    action_type=at,
                    description=f"Standard {at} response",
                    timeline="days",
                    likelihood=0.6,
                    reversibility="reversible",
                )
                for at in self.DEFAULT_ACTIONS
            ],
            reasoning=ResponseReasoning(
                personality_factors=[],
                historical_precedents=[],
                contextual_drivers=["null baseline: always predicts historical mean"],
                constraints=[],
                confidence=0.5,
            ),
            escalation_risk=self.MEAN_ESCALATION,
            de_escalation_openings=["Diplomatic channels"],
        )


class HeuristicPredictor:
    """Uses only event severity and type — no leader profile information.

    Tests whether leader profiles add information beyond what's already in the event.
    """

    SEVERITY_TO_ESCALATION = [
        (0.2, 0.15),
        (0.4, 0.35),
        (0.6, 0.50),
        (0.8, 0.70),
        (1.0, 0.85),
    ]

    CATEGORY_TO_ACTIONS = {
        "military": ["military", "diplomatic"],
        "diplomatic": ["diplomatic"],
        "economic": ["economic", "diplomatic"],
        "cyber": ["diplomatic", "information"],
        "information": ["diplomatic", "information"],
    }

    SEVERITY_TO_TONE = [
        (0.3, "measured"),
        (0.6, "defiant"),
        (0.8, "threatening"),
        (1.0, "threatening"),
    ]

    async def predict(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        escalation = self._severity_to_escalation(event.severity)
        actions = self.CATEGORY_TO_ACTIONS.get(event.structured.action_category, ["diplomatic"])
        tone = self._severity_to_tone(event.severity)

        if event.structured.reversibility in ("irreversible", "escalatory"):
            escalation = min(1.0, escalation + 0.1)

        return LeaderResponse(
            event_id=event.id,
            leader_id=leader.id,
            verbal_response=VerbalResponse(
                statement="Response based on event characteristics.",
                tone=tone,
                audience="domestic",
            ),
            actions=[
                LeaderAction(
                    action_type=at,
                    description=f"Heuristic {at} response to {event.structured.action_category} event",
                    timeline="days",
                    likelihood=0.7,
                    reversibility="reversible",
                )
                for at in actions
            ],
            reasoning=ResponseReasoning(
                personality_factors=[],
                historical_precedents=[],
                contextual_drivers=[f"severity={event.severity}", f"category={event.structured.action_category}"],
                constraints=["heuristic baseline: no leader profile used"],
                confidence=0.4,
            ),
            escalation_risk=escalation,
            de_escalation_openings=["Standard diplomatic channels"],
        )

    def _severity_to_escalation(self, severity: float) -> float:
        for threshold, value in self.SEVERITY_TO_ESCALATION:
            if severity <= threshold:
                return value
        return 0.85

    def _severity_to_tone(self, severity: float) -> str:
        for threshold, tone in self.SEVERITY_TO_TONE:
            if severity <= threshold:
                return tone
        return "threatening"


class GenericLLMPredictor:
    """Same LLM, same event details, but NO leader profile system prompt.

    Tests whether the structured leader profile adds value over the LLM's
    general geopolitical reasoning.
    """

    def __init__(self):
        self.provider = get_provider()

    async def predict(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        template = jinja_env.get_template("leader_event_no_profile.jinja2")
        prompt = template.render(event=event)

        system = "You are a geopolitical analyst making predictions about leader behavior based only on event details."

        try:
            raw_response = await self.provider.generate(system, prompt)
        except Exception as e:
            logger.error(f"GenericLLM baseline error: {e}")
            return self._fallback(leader, event)

        return self._parse(raw_response, leader, event)

    def _parse(self, raw: str, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        raw = raw.strip()
        if raw.startswith("```"):
            raw = raw.split("\n", 1)[1].rsplit("```", 1)[0]

        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            return self._fallback(leader, event)

        try:
            return LeaderResponse(
                event_id=event.id,
                leader_id=leader.id,
                verbal_response=VerbalResponse(**data.get("verbal_response", {
                    "statement": "No comment.", "tone": "measured", "audience": "domestic",
                })),
                actions=[LeaderAction(**a) for a in data.get("actions", [])],
                reasoning=ResponseReasoning(**data.get("reasoning", {
                    "personality_factors": [],
                    "historical_precedents": [],
                    "contextual_drivers": ["generic LLM baseline"],
                    "constraints": [],
                    "confidence": 0.4,
                })),
                escalation_risk=data.get("escalation_risk", 0.5),
                de_escalation_openings=data.get("de_escalation_openings", []),
            )
        except Exception:
            return self._fallback(leader, event)

    def _fallback(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        return LeaderResponse(
            event_id=event.id,
            leader_id=leader.id,
            verbal_response=VerbalResponse(
                statement="Situation under review.", tone="measured", audience="domestic",
            ),
            actions=[LeaderAction(
                action_type="diplomatic", description="Generic diplomatic response",
                timeline="days", likelihood=0.7, reversibility="reversible",
            )],
            reasoning=ResponseReasoning(
                personality_factors=[],
                historical_precedents=[],
                contextual_drivers=["generic LLM fallback"],
                constraints=["LLM response parse failure"],
                confidence=0.2,
            ),
            escalation_risk=event.severity * 0.7,
            de_escalation_openings=["Diplomatic channels"],
        )


class CognitiveOnlyPredictor:
    """Uses only the parametric cognitive decision engine — no LLM call.

    Tests whether the structured model alone is competitive with the LLM.
    """

    def __init__(self):
        from grms.services.cognitive_engine import CognitiveDecisionEngine
        self.engine = CognitiveDecisionEngine()

    ACTION_TO_TYPE = {
        "diplomatic_protest": "diplomatic",
        "economic_retaliation": "economic",
        "military_posturing": "military",
        "hybrid_pressure": "information",
        "direct_confrontation": "military",
    }

    ACTION_TO_TONE = {
        "diplomatic_protest": "measured",
        "economic_retaliation": "defiant",
        "military_posturing": "threatening",
        "hybrid_pressure": "measured",
        "direct_confrontation": "threatening",
    }

    async def predict(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        result = self.engine.predict(leader, event)

        primary_action = result.selected_action
        tone = self.ACTION_TO_TONE.get(primary_action, "measured")

        actions = []
        for action_score in result.action_scores[:3]:
            if action_score.final_score > 0:
                actions.append(LeaderAction(
                    action_type=self.ACTION_TO_TYPE.get(action_score.action, "diplomatic"),
                    description=f"Cognitive engine: {action_score.action.replace('_', ' ')}",
                    timeline="days",
                    likelihood=max(0.1, min(1.0, (action_score.final_score + 1) / 2)),
                    reversibility="reversible",
                ))

        if not actions:
            actions.append(LeaderAction(
                action_type="diplomatic",
                description="Default diplomatic response",
                timeline="days",
                likelihood=0.5,
                reversibility="reversible",
            ))

        top_drivers = []
        if result.action_scores:
            for contrib in result.action_scores[0].layer_contributions:
                if abs(contrib.weighted_contribution) > 0.02:
                    top_drivers.append(f"{contrib.layer}: {contrib.rationale}")

        return LeaderResponse(
            event_id=event.id,
            leader_id=leader.id,
            verbal_response=VerbalResponse(
                statement=f"Response determined by cognitive model: {primary_action.replace('_', ' ')}",
                tone=tone,
                audience="domestic",
            ),
            actions=actions,
            reasoning=ResponseReasoning(
                personality_factors=[f"cognitive_engine.{result.selected_action}"],
                historical_precedents=[],
                contextual_drivers=top_drivers[:3],
                constraints=["parametric model only — no LLM inference"],
                confidence=result.confidence,
            ),
            escalation_risk=result.escalation_estimate,
            de_escalation_openings=[],
            cognitive_trace=result,
        )
