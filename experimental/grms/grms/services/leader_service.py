"""Leader prediction service - LLM-driven geopolitical response modeling."""

import json
import logging
from pathlib import Path

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

logger = logging.getLogger("grms.leader_service")

PROMPTS_DIR = Path(__file__).parent.parent / "prompts"
jinja_env = Environment(loader=FileSystemLoader(str(PROMPTS_DIR)))


class LeaderService:
    def __init__(self):
        self.provider = get_provider()

    async def predict_hybrid(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        """Run cognitive engine first, then use its output as a structured prior for the LLM."""
        from grms.services.cognitive_engine import CognitiveDecisionEngine

        engine = CognitiveDecisionEngine()
        cognitive_result = engine.predict(leader, event)

        system_prompt = self._build_system_prompt(leader)
        hybrid_template = jinja_env.get_template("leader_event_with_prior.jinja2")
        user_prompt = hybrid_template.render(
            context=leader.decision_context,
            event=event,
            cognitive_result=cognitive_result,
        )

        try:
            raw_response = await self.provider.generate(system_prompt, user_prompt)
        except Exception as e:
            logger.error(f"Hybrid LLM error: {e}")
            return self._fallback_response(leader, event)

        parsed = self._parse_response(raw_response, leader, event)
        validated = self._validate_citations(parsed, leader)

        validated.cognitive_trace = cognitive_result

        esc_diff = abs(validated.escalation_risk - cognitive_result.escalation_estimate)
        action_overlap = self._compute_action_overlap(validated, cognitive_result)
        validated.hybrid_divergence = (esc_diff + (1.0 - action_overlap)) / 2.0

        if validated.hybrid_divergence > 0.4:
            validated.reasoning.confidence = min(validated.reasoning.confidence, 0.5)

        return validated

    def _compute_action_overlap(self, response: LeaderResponse, cognitive_result) -> float:
        """How much the LLM's predicted actions overlap with the cognitive engine's top actions."""
        llm_types = set(a.action_type for a in response.actions)
        action_to_type = {
            "diplomatic_protest": "diplomatic",
            "economic_retaliation": "economic",
            "military_posturing": "military",
            "hybrid_pressure": "information",
            "direct_confrontation": "military",
        }
        cognitive_types = set()
        for action_score in cognitive_result.action_scores[:3]:
            if action_score.final_score > 0:
                cognitive_types.add(action_to_type.get(action_score.action, "diplomatic"))

        if not llm_types or not cognitive_types:
            return 0.5
        intersection = len(llm_types & cognitive_types)
        union = len(llm_types | cognitive_types)
        return intersection / union if union > 0 else 0.0

    async def predict(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        system_prompt = self._build_system_prompt(leader)
        user_prompt = self._build_event_prompt(leader, event)

        try:
            raw_response = await self.provider.generate(system_prompt, user_prompt)
        except Exception as e:
            logger.error(f"LLM provider error: {e}")
            return self._fallback_response(leader, event)

        parsed = self._parse_response(raw_response, leader, event)
        validated = self._validate_citations(parsed, leader)
        return validated

    def _build_system_prompt(self, leader: LeaderProfile) -> str:
        template = jinja_env.get_template("leader_system.jinja2")
        return template.render(leader=leader)

    def _build_event_prompt(self, leader: LeaderProfile, event: GeopoliticalEvent) -> str:
        template = jinja_env.get_template("leader_event.jinja2")
        return template.render(context=leader.decision_context, event=event)

    def _parse_response(self, raw: str, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        raw = raw.strip()
        if raw.startswith("```"):
            raw = raw.split("\n", 1)[1].rsplit("```", 1)[0]

        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            logger.warning("LLM returned invalid JSON, using fallback response")
            return self._fallback_response(leader, event)

        try:
            escalation_risk = data.get("escalation_risk", 0.5)
            escalation_risk = self._enforce_escalation_floor(escalation_risk, event, leader)

            return LeaderResponse(
                event_id=event.id,
                leader_id=leader.id,
                verbal_response=VerbalResponse(**data.get("verbal_response", {
                    "statement": "No comment at this time.",
                    "tone": "measured",
                    "audience": "domestic",
                })),
                actions=[LeaderAction(**a) for a in data.get("actions", [])],
                reasoning=ResponseReasoning(**data.get("reasoning", {
                    "personality_factors": [],
                    "historical_precedents": [],
                    "contextual_drivers": [],
                    "constraints": ["insufficient profile data"],
                    "confidence": 0.3,
                })),
                escalation_risk=escalation_risk,
                de_escalation_openings=data.get("de_escalation_openings", []),
            )
        except Exception as e:
            logger.warning(f"Failed to parse LLM response structure: {e}")
            return self._fallback_response(leader, event)

    def _enforce_escalation_floor(self, escalation_risk: float, event: GeopoliticalEvent, leader: LeaderProfile) -> float:
        # A defender/target may legitimately de-escalate (surrender, negotiation)
        if event.structured.target == leader.country:
            return escalation_risk

        reversibility = event.structured.reversibility
        is_irreversible_military = reversibility in ("irreversible", "escalatory")

        if not is_irreversible_military:
            return escalation_risk

        # Scale floor with severity: sev 0.8 -> floor 0.7, sev 1.0 -> floor 0.85
        if event.severity >= 0.8:
            floor = 0.7 + (event.severity - 0.8) * 0.75
            if escalation_risk < floor:
                logger.info(
                    f"Escalation risk {escalation_risk:.2f} below floor {floor:.2f} "
                    f"(severity={event.severity}, reversibility={reversibility}); clamping"
                )
                return floor

        return escalation_risk

    def _validate_citations(self, response: LeaderResponse, leader: LeaderProfile) -> LeaderResponse:
        valid_dimensions = {
            "risk_tolerance", "authoritarianism", "nationalism", "pragmatism",
            "aggression", "populism", "transparency", "religiosity",
        }

        cited_valid = 0
        for factor in response.reasoning.personality_factors:
            for dim in valid_dimensions:
                if dim in factor.lower():
                    cited_valid += 1
                    break

        has_precedent_citations = len(response.reasoning.historical_precedents) > 0
        has_personality_citations = cited_valid > 0

        if not has_precedent_citations and not has_personality_citations:
            response.reasoning.confidence = min(response.reasoning.confidence, 0.3)
        elif not has_precedent_citations and response.reasoning.confidence > 0.8:
            response.reasoning.confidence = min(response.reasoning.confidence, 0.6)

        return response

    def _fallback_response(self, leader: LeaderProfile, event: GeopoliticalEvent) -> LeaderResponse:
        aggression = leader.personality.aggression
        tone = "threatening" if aggression > 0.3 else "measured" if aggression > -0.3 else "conciliatory"

        return LeaderResponse(
            event_id=event.id,
            leader_id=leader.id,
            verbal_response=VerbalResponse(
                statement=f"The government of {leader.country} is monitoring the situation closely.",
                tone=tone,
                audience="domestic",
            ),
            actions=[
                LeaderAction(
                    action_type="diplomatic",
                    description="Issue formal diplomatic communication",
                    timeline="hours",
                    likelihood=0.8,
                    reversibility="reversible",
                )
            ],
            reasoning=ResponseReasoning(
                personality_factors=[f"personality.aggression={aggression} informs tone"],
                historical_precedents=[],
                contextual_drivers=["LLM inference unavailable; using profile-based fallback"],
                constraints=["Limited to heuristic response due to LLM error"],
                confidence=0.2,
            ),
            escalation_risk=min(1.0, max(event.severity, (aggression + 1) / 2 * event.severity + event.severity * 0.5)),
            de_escalation_openings=["Diplomatic channels remain open"],
        )
