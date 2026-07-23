"""Parametric population response model - no LLM, runs in milliseconds."""

import math

import numpy as np

from grms.models.events import GeopoliticalEvent
from grms.models.population import PopulationProfile, PopulationSegment
from grms.models.responses import (
    InformationSpread,
    PopulationOverall,
    PopulationResponse,
    PopulationTrajectory,
    SegmentResponse,
)


class PopulationService:
    def predict(
        self,
        population: PopulationProfile,
        event: GeopoliticalEvent,
        leader_action_summary: str = "",
    ) -> PopulationResponse:
        segment_responses = []
        for segment in population.segments:
            sr = self._compute_segment_response(segment, event, leader_action_summary)
            segment_responses.append(sr)

        overall = self._aggregate(population.segments, segment_responses)
        info_spread = self._compute_information_spread(population, event, segment_responses)
        trajectory = self._compute_trajectory(overall, population, event)

        return PopulationResponse(
            event_id=event.id,
            country=population.country,
            overall=overall,
            segment_responses=segment_responses,
            information_spread=info_spread,
            trajectory=trajectory,
        )

    def _compute_segment_response(
        self,
        segment: PopulationSegment,
        event: GeopoliticalEvent,
        leader_action_summary: str,
    ) -> SegmentResponse:
        params = segment.response_params
        disp = segment.disposition
        baseline = disp.government_trust

        is_external_threat = event.structured.actor != event.structured.target
        rally_effect = params.rally_around_flag_coefficient * event.severity * (1.0 if is_external_threat else 0.0)

        economic_pain = params.economic_sensitivity * self._event_economic_impact(event)

        fatigue = 0.0

        action_alignment = self._compute_alignment(segment, event, leader_action_summary)

        support = np.clip(baseline + rally_effect - economic_pain - fatigue + action_alignment, 0.0, 1.0)
        oppose = np.clip((1.0 - support) * (1.0 - disp.compliance_baseline), 0.0, 1.0)
        neutral = np.clip(1.0 - support - oppose, 0.0, 1.0)

        protest = disp.protest_propensity * oppose * self._mobilization_factor(segment)
        amplification = params.amplification_factor * disp.social_media_activity

        concerns = self._infer_concerns(segment, event)

        return SegmentResponse(
            segment_name=segment.name,
            support=float(support),
            oppose=float(oppose),
            neutral=float(neutral),
            amplification=float(amplification),
            protest_propensity=float(np.clip(protest, 0.0, 1.0)),
            key_concerns=concerns,
        )

    def _aggregate(
        self,
        segments: list[PopulationSegment],
        responses: list[SegmentResponse],
    ) -> PopulationOverall:
        support = sum(s.percentage * r.support for s, r in zip(segments, responses))
        oppose = sum(s.percentage * r.oppose for s, r in zip(segments, responses))
        neutral = sum(s.percentage * r.neutral for s, r in zip(segments, responses))
        protest = sum(s.percentage * r.protest_propensity for s, r in zip(segments, responses))
        compliance = sum(s.percentage * s.disposition.compliance_baseline for s in segments)

        total = support + oppose + neutral
        if total > 0:
            support /= total
            oppose /= total
            neutral /= total

        return PopulationOverall(
            support_leader=float(np.clip(support, 0.0, 1.0)),
            oppose_leader=float(np.clip(oppose, 0.0, 1.0)),
            neutral=float(np.clip(neutral, 0.0, 1.0)),
            protest_likelihood=float(np.clip(protest, 0.0, 1.0)),
            compliance_likelihood=float(np.clip(compliance, 0.0, 1.0)),
        )

    def _compute_information_spread(
        self,
        population: PopulationProfile,
        event: GeopoliticalEvent,
        responses: list[SegmentResponse],
    ) -> InformationSpread:
        max_amplification = max(r.amplification for r in responses) if responses else 0.0
        visibility_mult = {"public": 1.0, "leaked": 0.6, "covert": 0.2}.get(event.structured.visibility, 0.5)
        viral_prob = float(np.clip(max_amplification * visibility_mult * event.severity, 0.0, 1.0))

        avg_media = np.mean([s.disposition.media_exposure for s in population.segments]) if population.segments else 0.5
        base_hours = 48.0
        saturation = base_hours / max(avg_media * visibility_mult, 0.1)

        return InformationSpread(
            viral_probability=viral_prob,
            dominant_narrative=f"Response to {event.structured.actor}'s {event.event_type}",
            counter_narratives=[],
            time_to_saturation_hours=float(np.clip(saturation, 1.0, 720.0)),
        )

    def _compute_trajectory(
        self,
        overall: PopulationOverall,
        population: PopulationProfile,
        event: GeopoliticalEvent,
    ) -> PopulationTrajectory:
        avg_fatigue_rate = np.mean([s.response_params.fatigue_rate for s in population.segments]) if population.segments else 0.1

        support_7d = overall.support_leader - avg_fatigue_rate * math.log(1 + 7) * 0.1
        support_30d = overall.support_leader - avg_fatigue_rate * math.log(1 + 30) * 0.1
        fatigue_days = int(7.0 / max(avg_fatigue_rate, 0.01))

        return PopulationTrajectory(
            support_7d=float(np.clip(support_7d, 0.0, 1.0)),
            support_30d=float(np.clip(support_30d, 0.0, 1.0)),
            fatigue_onset_days=min(fatigue_days, 365),
        )

    def _event_economic_impact(self, event: GeopoliticalEvent) -> float:
        if event.event_type == "economic":
            return event.severity * 0.8
        if event.structured.action_category in ("sanctions", "blockade", "trade_war"):
            return event.severity * 0.6
        return event.severity * 0.1

    def _mobilization_factor(self, segment: PopulationSegment) -> float:
        urban = segment.demographics.urbanization
        social = segment.disposition.social_media_activity
        return float(np.clip(urban * 0.6 + social * 0.4, 0.0, 1.0))

    def _compute_alignment(self, segment: PopulationSegment, event: GeopoliticalEvent, leader_action: str) -> float:
        nationalism = segment.disposition.nationalism
        is_defensive = event.structured.target == "self" or event.structured.reversibility == "irreversible"
        if is_defensive and nationalism > 0.5:
            return 0.1 * nationalism
        if "military" in leader_action.lower() and nationalism < 0.3:
            return -0.1
        return 0.0

    def _infer_concerns(self, segment: PopulationSegment, event: GeopoliticalEvent) -> list[str]:
        concerns = []
        if segment.response_params.economic_sensitivity > 0.6:
            concerns.append("economic impact")
        if segment.disposition.protest_propensity > 0.4:
            concerns.append("civil liberties")
        if event.event_type == "military_action":
            concerns.append("security")
        if segment.demographics.economic_class == "lower":
            concerns.append("basic needs")
        return concerns[:3]
