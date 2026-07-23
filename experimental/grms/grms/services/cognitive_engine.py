"""Cognitive Decision Engine — 6-layer parametric model for explainable predictions.

Refactored from cognitive_decision_demo.py into a reusable service that takes
LeaderProfile + GeopoliticalEvent and produces auditable decision traces.
"""

import math

from grms.models.cognitive import (
    ActionScore,
    CognitiveDecisionResult,
    InteractionEffect,
    LayerContribution,
)
from grms.models.events import GeopoliticalEvent
from grms.models.leader import LeaderProfile

ACTIONS = [
    "diplomatic_protest",
    "economic_retaliation",
    "military_posturing",
    "hybrid_pressure",
    "direct_confrontation",
]

ACTION_AFFORDANCES = {
    "diplomatic_protest":   {"escalation": -0.8, "strength": -0.5, "sovereignty": 0.2, "risk": -0.9, "visibility": 0.3},
    "economic_retaliation": {"escalation": -0.2, "strength":  0.3, "sovereignty": 0.6, "risk": -0.3, "visibility": 0.5},
    "military_posturing":   {"escalation":  0.4, "strength":  0.7, "sovereignty": 0.8, "risk":  0.3, "visibility": 0.8},
    "hybrid_pressure":      {"escalation":  0.3, "strength":  0.5, "sovereignty": 0.5, "risk":  0.1, "visibility": -0.3},
    "direct_confrontation": {"escalation":  0.9, "strength":  1.0, "sovereignty": 1.0, "risk":  0.9, "visibility": 1.0},
}

LAYER_WEIGHTS = {
    "identity":    0.10,
    "personality": 0.25,
    "belief":      0.25,
    "motivation":  0.15,
    "context":     0.15,
    "history":     0.10,
}

INTERACTION_TERMS = [
    {"layers": ["personality.aggression", "belief.hostile_intent"], "weight": 0.08},
    {"layers": ["personality.nationalism", "context.red_line_triggered"], "weight": 0.10},
    {"layers": ["motivation.power", "context.approval_rating"], "weight": 0.05},
]


class CognitiveDecisionEngine:
    def predict(self, leader: LeaderProfile, event: GeopoliticalEvent) -> CognitiveDecisionResult:
        red_line_triggered = self._check_red_line(leader, event)
        beliefs = self._infer_beliefs(leader, event)
        motivations = self._infer_motivations(leader)

        layer_scores: dict[str, dict[str, float]] = {}
        layer_rationales: dict[str, dict[str, str]] = {}

        layer_scores["identity"], layer_rationales["identity"] = self._score_identity(leader, red_line_triggered)
        layer_scores["personality"], layer_rationales["personality"] = self._score_personality(leader)
        layer_scores["belief"], layer_rationales["belief"] = self._score_belief(beliefs)
        layer_scores["motivation"], layer_rationales["motivation"] = self._score_motivation(motivations)
        layer_scores["context"], layer_rationales["context"] = self._score_context(leader, event, red_line_triggered)
        layer_scores["history"], layer_rationales["history"] = self._score_history(leader, beliefs)

        interactions = self._compute_interactions(leader, beliefs, motivations, red_line_triggered)

        final_scores: dict[str, float] = {action: 0.0 for action in ACTIONS}
        for layer_name, scores in layer_scores.items():
            weight = LAYER_WEIGHTS[layer_name]
            for action in ACTIONS:
                final_scores[action] += weight * scores[action]
        for interaction in interactions:
            for action in ACTIONS:
                final_scores[action] += interaction["effects"].get(action, 0.0)

        sorted_actions = sorted(final_scores.items(), key=lambda x: x[1], reverse=True)
        selected = sorted_actions[0][0]
        top_score = sorted_actions[0][1]
        runner_up = sorted_actions[1][1] if len(sorted_actions) > 1 else 0.0
        confidence = min(1.0, max(0.0, (top_score - runner_up) / max(0.01, abs(top_score))))

        # Weighted escalation across all actions (softmax-like weighting by score)
        # This ensures personality shifts produce continuous escalation changes
        # even when the top action doesn't change
        min_score = min(s for _, s in sorted_actions)
        shifted = [(a, s - min_score + 0.01) for a, s in sorted_actions]
        total_weight = sum(s for _, s in shifted)
        weighted_escalation = sum(
            (s / total_weight) * ACTION_AFFORDANCES[a]["escalation"]
            for a, s in shifted
        )
        # Map from [-1, +1] affordance range to [0, 1] escalation, scaled by event severity
        escalation_estimate = max(0.0, min(1.0, (weighted_escalation + 1.0) / 2.0 * event.severity + event.severity * 0.2))

        action_results = []
        for rank, (action, score) in enumerate(sorted_actions, 1):
            contributions = []
            for layer_name in LAYER_WEIGHTS:
                raw = layer_scores[layer_name][action]
                w = LAYER_WEIGHTS[layer_name]
                contributions.append(LayerContribution(
                    layer=layer_name,
                    weight=w,
                    raw_score=raw,
                    weighted_contribution=w * raw,
                    rationale=layer_rationales[layer_name].get(action, ""),
                ))
            i_effects = [inter["effects"].get(action, 0.0) for inter in interactions]
            action_results.append(ActionScore(
                action=action,
                final_score=score,
                layer_contributions=contributions,
                interaction_effects=i_effects,
                rank=rank,
            ))

        interaction_results = []
        for inter in interactions:
            interaction_results.append(InteractionEffect(
                description=inter["description"],
                weight=inter["weight"],
                magnitude=inter["magnitude"],
                effect_on_selected=inter["effects"].get(selected, 0.0),
            ))

        return CognitiveDecisionResult(
            selected_action=selected,
            confidence=confidence,
            escalation_estimate=escalation_estimate,
            action_scores=action_results,
            interaction_effects=interaction_results,
        )

    def _check_red_line(self, leader: LeaderProfile, event: GeopoliticalEvent) -> bool:
        if not leader.ideology.red_lines:
            return False
        desc_lower = event.description.lower()
        for red_line in leader.ideology.red_lines:
            keywords = [w.lower() for w in red_line.split() if len(w) > 3]
            if sum(1 for kw in keywords if kw in desc_lower) >= len(keywords) * 0.4:
                return True
        if event.severity >= 0.8 and event.structured.reversibility in ("irreversible", "escalatory"):
            return True
        return False

    def _infer_beliefs(self, leader: LeaderProfile, event: GeopoliticalEvent) -> dict[str, float]:
        actor_is_adversary = event.structured.actor.lower() in [a.lower() for a in leader.ideology.adversaries]
        hostile_intent = 0.5
        if actor_is_adversary:
            hostile_intent = min(1.0, 0.6 + event.severity * 0.3)
        elif event.structured.reversibility == "irreversible":
            hostile_intent = min(1.0, 0.4 + event.severity * 0.4)

        military_effective = 0.5
        readiness = leader.decision_context.military_posture.readiness_level
        if readiness == "mobilized":
            military_effective = 0.75
        elif readiness == "elevated":
            military_effective = 0.6
        elif readiness == "peacetime":
            military_effective = 0.35

        diplomatic_viable = 0.5
        if event.structured.reversibility == "irreversible":
            diplomatic_viable = 0.2
        elif event.severity < 0.4:
            diplomatic_viable = 0.7

        domestic_support = leader.decision_context.approval_rating / 100.0
        escalation_controllable = 0.5
        if event.severity > 0.8:
            escalation_controllable = 0.3
        elif event.severity < 0.4:
            escalation_controllable = 0.7

        return {
            "hostile_intent": hostile_intent,
            "military_effective": military_effective,
            "diplomatic_viable": diplomatic_viable,
            "domestic_support": domestic_support,
            "escalation_controllable": escalation_controllable,
        }

    def _infer_motivations(self, leader: LeaderProfile) -> dict[str, float]:
        p = leader.personality
        return {
            "power": (p.authoritarianism + p.aggression) / 2.0,
            "independence": p.nationalism,
            "honor": (p.nationalism + p.aggression) / 2.0,
            "status": (p.populism + p.authoritarianism) / 2.0,
            "tranquility": -p.risk_tolerance,
            "order": (p.authoritarianism + p.pragmatism) / 2.0,
            "vengeance": p.aggression * 0.7,
        }

    def _score_identity(self, leader: LeaderProfile, red_line_triggered: bool) -> tuple[dict[str, float], dict[str, str]]:
        style = leader.cultural_context.decision_making_style
        scores = {}
        rationales = {}
        role_bias = {
            "diplomatic_protest": 0.4,
            "economic_retaliation": 0.3,
            "military_posturing": 0.2,
            "hybrid_pressure": -0.1,
            "direct_confrontation": -0.4,
        }
        if style == "unilateral":
            role_bias["direct_confrontation"] += 0.3
            role_bias["military_posturing"] += 0.2
            role_bias["diplomatic_protest"] -= 0.2

        red_line_boost = 0.2 if red_line_triggered else 0.0
        for action in ACTIONS:
            base = role_bias.get(action, 0.0)
            escalation = ACTION_AFFORDANCES[action]["escalation"]
            boost = red_line_boost * max(0, escalation + 0.5)
            scores[action] = max(-1.0, min(1.0, base + boost))
            parts = [f"style={style}"]
            if red_line_boost > 0 and boost > 0:
                parts.append("red_line_credibility_boost")
            rationales[action] = "; ".join(parts)
        return scores, rationales

    def _score_personality(self, leader: LeaderProfile) -> tuple[dict[str, float], dict[str, str]]:
        p = leader.personality
        axis_mapping = {
            "risk_tolerance": ("risk", p.risk_tolerance),
            "aggression": ("escalation", p.aggression),
            "nationalism": ("sovereignty", p.nationalism),
            "pragmatism": ("visibility", p.pragmatism),
        }
        scores = {}
        rationales = {}
        for action in ACTIONS:
            total = 0.0
            count = 0
            drivers = []
            for axis, (affordance_key, val) in axis_mapping.items():
                action_val = ACTION_AFFORDANCES[action][affordance_key]
                if axis == "pragmatism":
                    contrib = val * (-action_val)
                else:
                    contrib = val * action_val
                total += contrib
                count += 1
                if abs(contrib) > 0.15:
                    drivers.append(f"{axis}={val:+.1f}")
            scores[action] = max(-1.0, min(1.0, total / count))
            rationales[action] = ", ".join(drivers) if drivers else "weak signal"
        return scores, rationales

    def _score_belief(self, beliefs: dict[str, float]) -> tuple[dict[str, float], dict[str, str]]:
        scores = {}
        rationales = {}
        for action in ACTIONS:
            s = 0.0
            drivers = []
            escalation = ACTION_AFFORDANCES[action]["escalation"]

            hostile_contrib = (beliefs["hostile_intent"] - 0.5) * 2 * max(0, escalation + 0.3)
            s += hostile_contrib
            if abs(hostile_contrib) > 0.1:
                drivers.append(f"hostile_intent={beliefs['hostile_intent']:.2f}")

            if action in ("military_posturing", "direct_confrontation"):
                mil_contrib = (beliefs["military_effective"] - 0.5) * 1.5
                s += mil_contrib
                if abs(mil_contrib) > 0.1:
                    drivers.append(f"mil_effective={beliefs['military_effective']:.2f}")

            if action == "diplomatic_protest":
                dip_contrib = (beliefs["diplomatic_viable"] - 0.3) * 2.0
                s += dip_contrib
                if abs(dip_contrib) > 0.1:
                    drivers.append(f"diplo_viable={beliefs['diplomatic_viable']:.2f}")

            visibility = ACTION_AFFORDANCES[action]["visibility"]
            support_contrib = (beliefs["domestic_support"] - 0.5) * visibility
            s += support_contrib

            controllability = beliefs["escalation_controllable"]
            if escalation > 0.5:
                penalty = (1.0 - controllability) * escalation
                s -= penalty
                if penalty > 0.1:
                    drivers.append(f"esc_uncontrollable")

            scores[action] = max(-1.0, min(1.0, s / 2.5))
            rationales[action] = ", ".join(drivers) if drivers else "neutral beliefs"
        return scores, rationales

    def _score_motivation(self, motivations: dict[str, float]) -> tuple[dict[str, float], dict[str, str]]:
        motivation_alignment = {
            "diplomatic_protest":   {"power": -0.3, "independence": 0.0, "honor": 0.2, "status": 0.1, "tranquility": 0.6, "vengeance": -0.5},
            "economic_retaliation": {"power": 0.3,  "independence": 0.7, "honor": 0.4, "status": 0.3, "tranquility": 0.1, "vengeance": 0.4},
            "military_posturing":   {"power": 0.7,  "independence": 0.6, "honor": 0.7, "status": 0.8, "tranquility": -0.4, "vengeance": 0.3},
            "hybrid_pressure":      {"power": 0.5,  "independence": 0.4, "honor": -0.2, "status": 0.1, "tranquility": -0.1, "vengeance": 0.6},
            "direct_confrontation": {"power": 0.9,  "independence": 0.8, "honor": 0.5, "status": 0.9, "tranquility": -0.9, "vengeance": 0.8},
        }
        scores = {}
        rationales = {}
        for action in ACTIONS:
            alignment = motivation_alignment[action]
            total = 0.0
            count = 0
            drivers = []
            for dim, action_val in alignment.items():
                if dim in motivations:
                    norm_motivation = max(-1.0, min(1.0, motivations[dim]))
                    contrib = norm_motivation * action_val
                    total += contrib
                    count += 1
                    if abs(contrib) > 0.2:
                        drivers.append(f"{dim}={motivations[dim]:+.2f}")
            scores[action] = max(-1.0, min(1.0, total / max(count, 1)))
            rationales[action] = ", ".join(drivers) if drivers else "weak motivational signal"
        return scores, rationales

    def _score_context(self, leader: LeaderProfile, event: GeopoliticalEvent, red_line_triggered: bool) -> tuple[dict[str, float], dict[str, str]]:
        ctx = leader.decision_context
        scores = {}
        rationales = {}
        for action in ACTIONS:
            s = 0.0
            drivers = []
            escalation = ACTION_AFFORDANCES[action]["escalation"]

            approval_factor = (ctx.approval_rating - 50) / 50
            approval_contrib = approval_factor * max(0, escalation) * 0.5
            s += approval_contrib
            if abs(approval_contrib) > 0.05:
                drivers.append(f"approval={ctx.approval_rating:.0f}")

            if red_line_triggered:
                if escalation < 0:
                    s -= 0.4
                    drivers.append("red_line_penalizes_weak")
                else:
                    s += 0.3
                    drivers.append("red_line_demands_strength")

            readiness_map = {"peacetime": -0.3, "elevated": 0.2, "mobilized": 0.5}
            if action in ("military_posturing", "direct_confrontation"):
                readiness_bonus = readiness_map.get(ctx.military_posture.readiness_level, 0.0)
                s += readiness_bonus
                if abs(readiness_bonus) > 0.1:
                    drivers.append(f"readiness={ctx.military_posture.readiness_level}")

            opposition = ctx.domestic_pressures.opposition_strength
            s += (1.0 - opposition) * max(0, escalation) * 0.2

            inflation = ctx.economic_conditions.inflation
            if action == "direct_confrontation" and inflation > 5:
                penalty = (inflation / 20.0) * 0.3
                s -= penalty
                drivers.append(f"inflation_risk={inflation:.0f}%")

            scores[action] = max(-1.0, min(1.0, s))
            rationales[action] = ", ".join(drivers) if drivers else "neutral context"
        return scores, rationales

    def _score_history(self, leader: LeaderProfile, beliefs: dict[str, float]) -> tuple[dict[str, float], dict[str, str]]:
        action_keywords = {
            "diplomatic_protest": ["diplomatic", "protest", "negotiate", "negotiate"],
            "economic_retaliation": ["economic", "sanction", "trade", "retaliation"],
            "military_posturing": ["military", "deploy", "exercise", "postur", "pressure"],
            "hybrid_pressure": ["cyber", "hybrid", "proxy", "information", "covert"],
            "direct_confrontation": ["invad", "attack", "war", "confront", "blockade", "ultimatum"],
        }
        historical_matches: dict[str, int] = {a: 0 for a in ACTIONS}
        for decision in leader.decision_history:
            text = f"{decision.decision_taken} {decision.outcome}".lower()
            for action, keywords in action_keywords.items():
                if any(kw in text for kw in keywords):
                    historical_matches[action] += 1

        scores = {}
        rationales = {}
        for action in ACTIONS:
            s = 0.0
            drivers = []
            if historical_matches[action] > 0:
                s += 0.4
                drivers.append(f"precedent_match={historical_matches[action]}")
                s += 0.1
            else:
                s -= 0.15
                drivers.append("no_precedent")

            if action == "diplomatic_protest" and beliefs["hostile_intent"] > 0.8:
                s -= 0.3
                drivers.append("hostile_intent_undermines_diplomacy")

            scores[action] = max(-1.0, min(1.0, s))
            rationales[action] = ", ".join(drivers) if drivers else ""
        return scores, rationales

    def _compute_interactions(
        self,
        leader: LeaderProfile,
        beliefs: dict[str, float],
        motivations: dict[str, float],
        red_line_triggered: bool,
    ) -> list[dict]:
        results = []
        interaction_values = [
            ("personality.aggression × belief.hostile_intent", 0.08,
             leader.personality.aggression, beliefs["hostile_intent"]),
            ("personality.nationalism × context.red_line_triggered", 0.10,
             leader.personality.nationalism, 1.0 if red_line_triggered else 0.0),
            ("motivation.power × context.approval_rating", 0.05,
             motivations.get("power", 0.0), leader.decision_context.approval_rating / 100.0),
        ]

        for description, weight, val_a, val_b in interaction_values:
            magnitude = val_a * val_b
            effects = {}
            for action in ACTIONS:
                escalation = ACTION_AFFORDANCES[action]["escalation"]
                effects[action] = magnitude * max(0, escalation + 0.3) * weight
            results.append({
                "description": description,
                "weight": weight,
                "magnitude": magnitude,
                "effects": effects,
            })
        return results
