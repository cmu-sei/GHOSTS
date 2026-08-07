"""Fog of war — the reason an umpire exists.

Ground truth lives in WorldState; the player must not see all of it. When OPFOR
plays a move or a trigger fires, each emits *indicators* — the observable signatures
of what happened. The fog filter decides which of those indicators the player
actually perceives, given the scenario's fog policy:

  partial : the player perceives emitted indicators, never the underlying flags or
            facts. They must infer OPFOR intent from signals. (Kriegspiel default.)
  full    : strict — the player perceives nothing until an indicator surfaces it;
            here identical to partial for indicators, but ground truth is never
            narrated. (Reserved for stricter authored scenarios.)
  off     : training/debug — the player perceives every indicator immediately and
            ground truth may be shown.

Kept deliberately small: the policy is a pass-through decision on already-authored
indicators, not an inference engine. Smart default is partial.
"""

from __future__ import annotations

from .models import Scenario


def visible_indicators(scenario: Scenario, indicators: list[str]) -> list[str]:
    """The subset of freshly-emitted indicators the player perceives this turn."""
    policy = (scenario.fog.default or "partial").lower()
    if not indicators:
        return []
    if policy == "off":
        return list(indicators)
    # partial / full: the player perceives the emitted indicators (the observable
    # signatures) but never the ground-truth flags/facts behind them. Any authored
    # indicator that is explicitly marked hidden (prefixed "!") is withheld.
    return [i for i in indicators if not i.startswith("!")]


def reveals_ground_truth(scenario: Scenario) -> bool:
    """Whether the HUD may show ground-truth flags/facts (training/debug only)."""
    return (scenario.fog.default or "partial").lower() == "off"
