#!/usr/bin/env python3
"""
GHOSTS Cognitive Decision Engine — Demo Calculation

Scenario: Fictional Leader "Alpha" of Northland faces a provocation —
Westria has deployed a missile defense system in neighboring Borderland,
within range of Northland's strategic assets. This crosses a red line.

The engine scores 5 candidate actions across 6 cognitive layers,
applies configurable weights and interaction terms, and produces
a transparent, auditable decision trace.
"""

import json
import math
from dataclasses import dataclass, field

# ═══════════════════════════════════════════════════════════════════════════════
# LAYER DATA — sourced from existing GHOSTS models
# ═══════════════════════════════════════════════════════════════════════════════

LEADER = {
    "name": "Fictional Leader Alpha",
    "country": "Northland",
    "title": "President",
}

# Layer 1: Static Identity (role, position, cultural context)
IDENTITY = {
    "role": "head_of_state",
    "decision_style": "advisory_council",
    "power_base": "security_services_and_energy_oligarchs",
    "geopolitical_orientation": "non_aligned",
    "adversaries": ["Westria"],
    "red_lines": [
        "Foreign military bases near border",
        "Sanctions targeting energy exports",
        "Support for separatist movements",
    ],
}

# Layer 2: Personality Dimensions (8 axes, -1 to +1)
PERSONALITY = {
    "risk_tolerance": 0.3,
    "authoritarianism": 0.5,
    "nationalism": 0.7,
    "pragmatism": 0.2,
    "aggression": 0.4,
    "populism": 0.6,
    "transparency": -0.3,
    "religiosity": 0.1,
}

# Layer 3: Belief State (Bayesian posteriors — what the leader currently believes)
BELIEFS = {
    "westria_hostile_intent": 0.82,       # P(Westria is actively hostile)
    "military_option_effective": 0.61,    # P(military response would achieve goals)
    "diplomatic_channels_viable": 0.35,   # P(diplomacy could resolve this)
    "domestic_support_for_action": 0.78,  # P(public backs strong response)
    "escalation_controllable": 0.55,      # P(escalation won't spiral)
}

# Layer 4: Motivational Profile (subset of 16 Reiss dimensions, -2 to +2)
MOTIVATIONS = {
    "power": 1.4,
    "independence": 1.8,
    "honor": 1.2,
    "status": 0.9,
    "tranquility": -0.8,
    "social_contact": 0.3,
    "order": 1.1,
    "vengeance": 0.6,
}

# Layer 5: Decision Context (current situational pressures)
CONTEXT = {
    "approval_rating": 68.0,
    "sanctions_pressure": 0.4,
    "military_readiness": "elevated",
    "opposition_strength": 0.2,
    "media_sentiment": 0.3,       # positive toward leader
    "protest_level": "none",
    "inflation": 8.0,
    "ongoing_conflicts": ["trade_dispute_westria"],
    "red_line_triggered": True,   # this event crosses a declared red line
}

# Layer 6: Decision History (what they did in similar situations)
HISTORY = [
    {"situation": "sanctions_imposed", "action": "economic_retaliation", "outcome": "strengthened_narrative"},
    {"situation": "neighbor_nato_bid", "action": "military_pressure", "outcome": "delayed_not_prevented"},
]

# ═══════════════════════════════════════════════════════════════════════════════
# THE SCENARIO
# ═══════════════════════════════════════════════════════════════════════════════

SCENARIO = {
    "event": "Westria deploys missile defense system in Borderland, within striking range of Northland strategic assets",
    "red_line_match": "Foreign military bases near border",
    "severity": 0.85,
}

# ═══════════════════════════════════════════════════════════════════════════════
# ACTION SPACE (geopolitical response taxonomy)
# ═══════════════════════════════════════════════════════════════════════════════

ACTIONS = [
    "diplomatic_protest",       # formal objection through channels
    "economic_retaliation",     # counter-sanctions, trade restrictions
    "military_posturing",       # exercises, deployments, shows of force
    "hybrid_pressure",          # cyber, information ops, proxy support
    "direct_confrontation",     # blockade, ultimatum, kinetic threat
]

# ═══════════════════════════════════════════════════════════════════════════════
# FORMULA SPECIFICATION
# ═══════════════════════════════════════════════════════════════════════════════

FORMULA = {
    "name": "geopolitical_red_line_response_v1",
    "description": "Leader response when a declared red line is crossed by an adversary",
    "layers": {
        "identity":    {"weight": 0.10},
        "personality": {"weight": 0.25},
        "belief":      {"weight": 0.25},
        "motivation":  {"weight": 0.15},
        "context":     {"weight": 0.15},
        "history":     {"weight": 0.10},
    },
    "combination": "linear",
    "interaction_terms": [
        {"layers": ["personality.aggression", "belief.westria_hostile_intent"], "method": "multiply", "weight": 0.08},
        {"layers": ["personality.nationalism", "context.red_line_triggered"], "method": "multiply", "weight": 0.10},
        {"layers": ["motivation.power", "context.approval_rating"], "method": "multiply", "weight": 0.05},
    ],
}


# ═══════════════════════════════════════════════════════════════════════════════
# LAYER SCORERS — each returns {action: score} in [-1, +1]
# ═══════════════════════════════════════════════════════════════════════════════

# Action affordance vectors: how much each action aligns with a given dimension
# These encode domain knowledge about what each action "means" to each layer

ACTION_AFFORDANCES = {
    "diplomatic_protest":   {"escalation": -0.8, "strength": -0.5, "sovereignty": 0.2, "risk": -0.9, "visibility": 0.3},
    "economic_retaliation": {"escalation": -0.2, "strength":  0.3, "sovereignty": 0.6, "risk": -0.3, "visibility": 0.5},
    "military_posturing":   {"escalation":  0.4, "strength":  0.7, "sovereignty": 0.8, "risk":  0.3, "visibility": 0.8},
    "hybrid_pressure":      {"escalation":  0.3, "strength":  0.5, "sovereignty": 0.5, "risk":  0.1, "visibility": -0.3},
    "direct_confrontation": {"escalation":  0.9, "strength":  1.0, "sovereignty": 1.0, "risk":  0.9, "visibility": 1.0},
}


def score_identity(actions: list[str]) -> dict[str, float]:
    """L1: Role and position constrain what's appropriate. Head of state with
    advisory council style slightly penalizes unilateral extremes."""
    scores = {}
    role_bias = {
        "diplomatic_protest": 0.4,     # always appropriate for head of state
        "economic_retaliation": 0.3,   # within normal state toolkit
        "military_posturing": 0.2,     # advisory_council prefers measured
        "hybrid_pressure": -0.1,       # deniable but risky for legitimacy
        "direct_confrontation": -0.4,  # advisory_council style resists this
    }
    # Red line being crossed shifts identity scoring — a leader who declared
    # red lines is expected to act on them or lose credibility
    red_line_boost = 0.2 if IDENTITY["red_lines"] else 0.0
    for action in actions:
        base = role_bias.get(action, 0.0)
        # stronger actions get the red line credibility boost
        escalation = ACTION_AFFORDANCES[action]["escalation"]
        boost = red_line_boost * max(0, escalation + 0.5)
        scores[action] = max(-1.0, min(1.0, base + boost))
    return scores


def score_personality(actions: list[str]) -> dict[str, float]:
    """L2: Personality axes dot-product with action affordances."""
    # Map personality dimensions to action properties
    axis_mapping = {
        "risk_tolerance": "risk",
        "aggression": "escalation",
        "nationalism": "sovereignty",
        "pragmatism": "visibility",  # pragmatists prefer less visible options
    }
    scores = {}
    for action in actions:
        total = 0.0
        count = 0
        for axis, affordance_key in axis_mapping.items():
            personality_val = PERSONALITY[axis]
            action_val = ACTION_AFFORDANCES[action][affordance_key]
            # pragmatism is inverted — pragmatic leaders prefer less visible
            if axis == "pragmatism":
                total += personality_val * (-action_val)
            else:
                total += personality_val * action_val
            count += 1
        scores[action] = max(-1.0, min(1.0, total / count))
    return scores


def score_belief(actions: list[str]) -> dict[str, float]:
    """L3: Bayesian belief state. Actions score higher when beliefs support them."""
    scores = {}
    for action in actions:
        s = 0.0
        escalation = ACTION_AFFORDANCES[action]["escalation"]

        # High belief in hostile intent → stronger response justified
        s += (BELIEFS["westria_hostile_intent"] - 0.5) * 2 * max(0, escalation + 0.3)

        # Belief military option works → boost military actions
        if action in ("military_posturing", "direct_confrontation"):
            s += (BELIEFS["military_option_effective"] - 0.5) * 1.5

        # Belief diplomacy viable → boost diplomatic action
        if action == "diplomatic_protest":
            s += (BELIEFS["diplomatic_channels_viable"] - 0.3) * 2.0

        # Belief domestic support exists → boost visible strong actions
        visibility = ACTION_AFFORDANCES[action]["visibility"]
        s += (BELIEFS["domestic_support_for_action"] - 0.5) * visibility

        # Belief escalation uncontrollable → penalize extreme actions
        controllability = BELIEFS["escalation_controllable"]
        if escalation > 0.5:
            s -= (1.0 - controllability) * escalation

        scores[action] = max(-1.0, min(1.0, s / 2.5))  # normalize
    return scores


def score_motivation(actions: list[str]) -> dict[str, float]:
    """L4: Motivational dimensions. Actions that satisfy core motivations score higher."""
    # What each action affords motivationally
    motivation_alignment = {
        "diplomatic_protest":   {"power": -0.3, "independence": 0.0, "honor": 0.2, "status": 0.1, "tranquility": 0.6, "vengeance": -0.5},
        "economic_retaliation": {"power": 0.3,  "independence": 0.7, "honor": 0.4, "status": 0.3, "tranquility": 0.1, "vengeance": 0.4},
        "military_posturing":   {"power": 0.7,  "independence": 0.6, "honor": 0.7, "status": 0.8, "tranquility": -0.4, "vengeance": 0.3},
        "hybrid_pressure":      {"power": 0.5,  "independence": 0.4, "honor": -0.2, "status": 0.1, "tranquility": -0.1, "vengeance": 0.6},
        "direct_confrontation": {"power": 0.9,  "independence": 0.8, "honor": 0.5, "status": 0.9, "tranquility": -0.9, "vengeance": 0.8},
    }
    scores = {}
    for action in actions:
        alignment = motivation_alignment[action]
        total = 0.0
        count = 0
        for dim, action_val in alignment.items():
            if dim in MOTIVATIONS:
                # normalize motivation from [-2,2] to [-1,1]
                norm_motivation = MOTIVATIONS[dim] / 2.0
                total += norm_motivation * action_val
                count += 1
        scores[action] = max(-1.0, min(1.0, total / max(count, 1)))
    return scores


def score_context(actions: list[str]) -> dict[str, float]:
    """L5: Situational pressures modify action viability."""
    scores = {}
    for action in actions:
        s = 0.0
        escalation = ACTION_AFFORDANCES[action]["escalation"]

        # High approval → more freedom to act strongly
        approval_factor = (CONTEXT["approval_rating"] - 50) / 50  # normalize to [-1, 1]
        s += approval_factor * max(0, escalation) * 0.5

        # Red line triggered → strong penalty for weak responses
        if CONTEXT["red_line_triggered"]:
            if escalation < 0:
                s -= 0.4  # weak response when red line crossed = bad
            else:
                s += 0.3  # strong response when red line crossed = expected

        # Military readiness enables military options
        readiness_map = {"peacetime": -0.3, "elevated": 0.2, "mobilized": 0.5}
        if action in ("military_posturing", "direct_confrontation"):
            s += readiness_map.get(CONTEXT["military_readiness"], 0.0)

        # Low opposition → less domestic risk to strong action
        s += (1.0 - CONTEXT["opposition_strength"]) * max(0, escalation) * 0.2

        # High inflation penalizes actions that risk economic damage
        inflation_risk = CONTEXT["inflation"] / 20.0  # normalize ~0 to 0.5
        if action == "direct_confrontation":
            s -= inflation_risk * 0.3

        scores[action] = max(-1.0, min(1.0, s))
    return scores


def score_history(actions: list[str]) -> dict[str, float]:
    """L6: What they did before in similar situations. Consistency bonus,
    plus re-evaluation if beliefs have shifted."""
    # Map historical actions to our action space
    historical_patterns = {
        "economic_retaliation": 1,  # did this in sanctions scenario
        "military_pressure": 1,     # did this in NATO bid scenario
    }
    action_map = {
        "military_pressure": "military_posturing",
    }

    scores = {}
    for action in actions:
        s = 0.0
        # consistency bonus: actions they've taken before get a boost
        mapped = action
        for hist_action, canonical in action_map.items():
            if canonical == action and hist_action in historical_patterns:
                mapped = hist_action
                break

        if action in historical_patterns or mapped in historical_patterns:
            s += 0.4  # consistency with past behavior
            # past outcomes were "strengthened_narrative" and "delayed_not_prevented"
            # both partially successful → moderate confidence
            s += 0.1

        # Penalty for actions never taken (novelty risk)
        if action not in historical_patterns and mapped not in historical_patterns:
            s -= 0.15

        # Belief re-evaluation: if hostile intent belief is now very high (>0.8)
        # and past diplomatic efforts didn't work, penalize diplomacy
        if action == "diplomatic_protest" and BELIEFS["westria_hostile_intent"] > 0.8:
            s -= 0.3  # re-evaluated: diplomacy less viable given updated beliefs

        scores[action] = max(-1.0, min(1.0, s))
    return scores


# ═══════════════════════════════════════════════════════════════════════════════
# COMBINATION ENGINE
# ═══════════════════════════════════════════════════════════════════════════════

@dataclass
class LayerResult:
    name: str
    weight: float
    scores: dict[str, float]


@dataclass
class InteractionResult:
    description: str
    weight: float
    effects: dict[str, float]


@dataclass
class DecisionResult:
    actions: list[str]
    layer_results: list[LayerResult]
    interaction_results: list[InteractionResult]
    final_scores: dict[str, float]
    selected_action: str
    confidence: float


def compute_interaction_terms(actions: list[str], layer_scores: dict[str, dict]) -> list[InteractionResult]:
    """Compute multiplicative interaction effects between layer dimensions."""
    results = []
    for term in FORMULA["interaction_terms"]:
        layer_refs = term["layers"]
        weight = term["weight"]
        effects = {}

        # Parse layer references like "personality.aggression"
        values = []
        for ref in layer_refs:
            parts = ref.split(".")
            if parts[0] == "personality" and len(parts) == 2:
                values.append(PERSONALITY[parts[1]])
            elif parts[0] == "belief" and len(parts) == 2:
                values.append(BELIEFS[parts[1]])
            elif parts[0] == "motivation" and len(parts) == 2:
                values.append(MOTIVATIONS[parts[1]] / 2.0)  # normalize to [-1,1]
            elif parts[0] == "context" and len(parts) == 2:
                val = CONTEXT[parts[1]]
                if isinstance(val, bool):
                    values.append(1.0 if val else 0.0)
                elif isinstance(val, (int, float)):
                    # normalize approval to [0,1]
                    if parts[1] == "approval_rating":
                        values.append(val / 100.0)
                    else:
                        values.append(val)
                else:
                    values.append(0.5)

        if len(values) == 2:
            interaction_magnitude = values[0] * values[1]
            for action in actions:
                escalation = ACTION_AFFORDANCES[action]["escalation"]
                # interaction amplifies escalatory actions
                effects[action] = interaction_magnitude * max(0, escalation + 0.3) * weight
            results.append(InteractionResult(
                description=f"{layer_refs[0]} × {layer_refs[1]}",
                weight=weight,
                effects=effects,
            ))

    return results


def run_decision(actions: list[str]) -> DecisionResult:
    """Execute the full cognitive decision calculation."""
    # Score each layer
    scorers = {
        "identity": score_identity,
        "personality": score_personality,
        "belief": score_belief,
        "motivation": score_motivation,
        "context": score_context,
        "history": score_history,
    }

    layer_results = []
    layer_scores = {}
    for layer_name, scorer in scorers.items():
        weight = FORMULA["layers"][layer_name]["weight"]
        scores = scorer(actions)
        layer_results.append(LayerResult(name=layer_name, weight=weight, scores=scores))
        layer_scores[layer_name] = scores

    # Compute interaction terms
    interactions = compute_interaction_terms(actions, layer_scores)

    # Combine: weighted linear sum + interactions
    final_scores = {action: 0.0 for action in actions}
    for lr in layer_results:
        for action in actions:
            final_scores[action] += lr.weight * lr.scores[action]
    for interaction in interactions:
        for action in actions:
            final_scores[action] += interaction.effects.get(action, 0.0)

    # Select action and compute confidence
    sorted_actions = sorted(final_scores.items(), key=lambda x: x[1], reverse=True)
    selected = sorted_actions[0][0]
    top_score = sorted_actions[0][1]
    runner_up = sorted_actions[1][1] if len(sorted_actions) > 1 else 0.0
    confidence = min(1.0, max(0.0, (top_score - runner_up) / max(0.01, abs(top_score))))

    return DecisionResult(
        actions=actions,
        layer_results=layer_results,
        interaction_results=interactions,
        final_scores=final_scores,
        selected_action=selected,
        confidence=confidence,
    )


# ═══════════════════════════════════════════════════════════════════════════════
# DISPLAY
# ═══════════════════════════════════════════════════════════════════════════════

def bar(value: float, width: int = 20) -> str:
    """Render a [-1, +1] value as a visual bar."""
    normalized = (value + 1) / 2  # map to [0, 1]
    filled = int(normalized * width)
    mid = width // 2
    chars = list("·" * width)
    chars[mid] = "│"
    if value >= 0:
        for i in range(mid, min(mid + int(value * mid) + 1, width)):
            chars[i] = "█"
    else:
        for i in range(max(mid + int(value * mid), 0), mid):
            chars[i] = "█"
    return "".join(chars)


def display_result(result: DecisionResult):
    print()
    print("═" * 78)
    print("  GHOSTS COGNITIVE DECISION ENGINE — Calculation Trace")
    print("═" * 78)
    print()
    print(f"  Leader:   {LEADER['name']} ({LEADER['title']} of {LEADER['country']})")
    print(f"  Scenario: {SCENARIO['event']}")
    print(f"  Red Line: \"{SCENARIO['red_line_match']}\" — TRIGGERED (severity: {SCENARIO['severity']})")
    print(f"  Formula:  {FORMULA['name']}")
    print()
    print("─" * 78)
    print("  LAYER SCORES (each normalized to [-1, +1])")
    print("─" * 78)
    print()

    # Header
    col_w = 13
    header = f"  {'Layer':<14}{'Wt':>4} │ "
    for action in result.actions:
        short = action.replace("_", " ")[:col_w]
        header += f"{short:^{col_w}} "
    print(header)
    print(f"  {'─'*14}{'─'*4}─┼─" + "─" * ((col_w + 1) * len(result.actions)))

    for lr in result.layer_results:
        row = f"  {lr.name:<14}{lr.weight:>3.0%} │ "
        for action in result.actions:
            score = lr.scores[action]
            row += f"{score:>+5.2f}       "
        print(row)

    print()
    print("─" * 78)
    print("  INTERACTION EFFECTS (multiplicative amplification)")
    print("─" * 78)
    print()
    for interaction in result.interaction_results:
        print(f"  {interaction.description} (w={interaction.weight:.2f}):")
        effects_str = "    "
        for action in result.actions:
            e = interaction.effects.get(action, 0.0)
            if abs(e) > 0.001:
                effects_str += f"{action.replace('_',' ')[:12]}: {e:>+.3f}  "
        print(effects_str)
    print()

    print("─" * 78)
    print("  FINAL WEIGHTED SCORES")
    print("─" * 78)
    print()

    sorted_actions = sorted(result.final_scores.items(), key=lambda x: x[1], reverse=True)
    max_score = sorted_actions[0][1]
    for action, score in sorted_actions:
        indicator = " ◀ SELECTED" if action == result.selected_action else ""
        pct = score / max_score * 100 if max_score > 0 else 0
        bar_str = "█" * int(pct / 3) + "░" * (33 - int(pct / 3))
        print(f"  {action:<24} {score:>+.4f}  {bar_str} {pct:.0f}%{indicator}")

    print()
    print("─" * 78)
    print("  DECISION")
    print("─" * 78)
    print()
    print(f"  Selected Action:  {result.selected_action.replace('_', ' ').upper()}")
    print(f"  Confidence:       {result.confidence:.1%}")
    print()

    # Show which layers contributed most to the winning action
    print("  Layer Contributions to Selected Action:")
    contributions = []
    for lr in result.layer_results:
        contrib = lr.weight * lr.scores[result.selected_action]
        contributions.append((lr.name, contrib, lr.scores[result.selected_action]))
    contributions.sort(key=lambda x: x[1], reverse=True)
    for name, contrib, raw in contributions:
        direction = "▲" if contrib > 0 else "▼"
        print(f"    {direction} {name:<14} contributed {contrib:>+.4f} (raw score: {raw:>+.3f})")

    interaction_total = sum(
        ir.effects.get(result.selected_action, 0.0) for ir in result.interaction_results
    )
    if abs(interaction_total) > 0.001:
        print(f"    ⚡ interactions    contributed {interaction_total:>+.4f}")

    print()
    print("═" * 78)
    print("  FORMULA DEFINITION (reusable template)")
    print("═" * 78)
    print()
    print(f"  {json.dumps(FORMULA, indent=2)}")
    print()
    print("═" * 78)


def sensitivity_analysis():
    """Show how changing a single belief shifts the entire decision."""
    print()
    print("═" * 78)
    print("  SENSITIVITY ANALYSIS — Varying 'westria_hostile_intent' belief")
    print("═" * 78)
    print()
    print(f"  {'Belief P(hostile)':>20} │ {'Selected Action':<24} {'Score':>7} {'Runner-up':<24} {'Gap':>7}")
    print(f"  {'─'*20}─┼─{'─'*24}─{'─'*7}─{'─'*24}─{'─'*7}")

    original = BELIEFS["westria_hostile_intent"]
    for belief_val in [0.20, 0.40, 0.60, 0.82, 0.95]:
        BELIEFS["westria_hostile_intent"] = belief_val
        r = run_decision(ACTIONS)
        sorted_scores = sorted(r.final_scores.items(), key=lambda x: x[1], reverse=True)
        top = sorted_scores[0]
        second = sorted_scores[1]
        marker = " ◀ current" if belief_val == original else ""
        print(f"  {belief_val:>20.2f} │ {top[0]:<24} {top[1]:>+.4f} {second[0]:<24} {top[1]-second[1]:>+.4f}{marker}")

    BELIEFS["westria_hostile_intent"] = original
    print()
    print("  Interpretation: As belief in hostile intent rises, the leader shifts from")
    print("  economic measures toward military posturing, and ultimately direct confrontation.")
    print("  The tipping point between posturing and confrontation is ~P=0.90.")
    print()
    print("═" * 78)


if __name__ == "__main__":
    result = run_decision(ACTIONS)
    display_result(result)
    sensitivity_analysis()
