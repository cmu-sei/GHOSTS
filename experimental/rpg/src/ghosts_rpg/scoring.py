"""Deterministic after-action review.

Derives win/loss and a grade from canonical engine state — objective statuses plus
the branch the timeline actually took. Not LLM-judged (DESIGN.md §6 step 6)."""

from __future__ import annotations

from dataclasses import dataclass, field

from .engine import Engine


@dataclass
class Aar:
    outcome: str  # "WIN" | "LOSS" | "INCOMPLETE"
    grade: str  # A/B/C/D/F
    score: int  # 0-100
    objectives_met: int
    objectives_total: int
    minutes_spent: int = 0
    lunch_minutes: int = 0
    made_lunch: bool = True
    highlights: list[str] = field(default_factory=list)


def review(engine: Engine) -> Aar:
    objs = engine.bundle.objectives
    total = len(objs)
    met = sum(1 for o in objs if engine.state.objective_status.get(o.id) == "Achieved")

    # WIN = the player steered onto every favorable branch, i.e. set every steering
    # flag the scenario tracks (phishing: 'contained'; soc-morning: 'ransomware-
    # contained'). Data-driven so each scenario defines its own containment.
    contained = bool(engine.known_flags) and engine.known_flags <= engine.state.flags
    outcome = "INCOMPLETE"
    if engine.state.is_complete:
        outcome = "WIN" if contained else "LOSS"

    # The lunch clock: clearing the worklist before the window runs out earns the
    # urgency bonus; working through lunch costs it. A detonation forfeits lunch
    # outright — you're now working an incident, not eating.
    spent = engine.state.minutes_spent
    budget = engine.lunch_minutes
    made_lunch = spent <= budget and not engine.state.detonated

    # A detonation is a hard failure: the threat encrypted the share before you
    # contained it. Whatever triage you did doesn't redeem the outcome — cap it in
    # the F range and don't award the made-lunch bonus for a morning you lost.
    detonated = engine.state.detonated
    coverage = (met / total) if total else 0.0
    if detonated:
        score = round(coverage * 25)  # partial triage credit only, F-range ceiling
    else:
        score = round(coverage * 65 + (15 if contained else 0) + (10 if engine.state.is_complete else 0) + (10 if made_lunch else 0))
    score = max(0, min(100, score))
    grade = _grade(score)

    highlights: list[str] = []
    for o in objs:
        done = engine.state.objective_status.get(o.id) == "Achieved"
        highlights.append(f"[{'x' if done else ' '}] {o.name}")
    if engine.state.is_complete:
        if detonated:
            highlights.append(
                f"The decision deadline expired at the {engine.containment_deadline}m mark."
            )
        else:
            highlights.append(
                "Secured the scenario's favorable branch."
                if contained
                else "Missed the decisive condition; the unfavorable branch fired."
            )
            highlights.append(
                f"Cleared the worklist in {spent}m of the {budget}m exercise window."
                if made_lunch
                else f"Used the full {budget}m exercise window."
            )
        if engine.state.assumptions:
            highlights.append(
                "Assumptions tested: " + "; ".join(engine.state.assumptions[:3])
            )
        if engine.state.umpire_findings:
            highlights.append(
                "Schlussbesprechung: " + " | ".join(engine.state.umpire_findings[-3:])
            )
    return Aar(
        outcome=outcome,
        grade=grade,
        score=score,
        objectives_met=met,
        objectives_total=total,
        minutes_spent=spent,
        lunch_minutes=budget,
        made_lunch=made_lunch,
        highlights=highlights,
    )


def _grade(score: int) -> str:
    if score >= 90:
        return "A"
    if score >= 80:
        return "B"
    if score >= 70:
        return "C"
    if score >= 60:
        return "D"
    return "F"
