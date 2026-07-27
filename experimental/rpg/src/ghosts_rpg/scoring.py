"""Deterministic after-action review.

Derives the grade from canonical engine state — the outcome the engine already set
(WIN/LOSS/INCOMPLETE), objective coverage, whether OPFOR was denied, and the clock.
Not LLM-judged. The debrief surfaces the odds/roll history so the player can see how
the umpire read each of their moves."""

from __future__ import annotations

from dataclasses import dataclass, field

from .engine import Engine


@dataclass
class Aar:
    outcome: str  # WIN | LOSS | INCOMPLETE
    grade: str  # A/B/C/D/F
    score: int  # 0-100
    objectives_met: int
    objectives_total: int
    minutes_spent: int = 0
    window_minutes: int = 0
    opfor_progress: int = 0
    opfor_threshold: int = 0
    highlights: list[str] = field(default_factory=list)


def review(engine: Engine) -> Aar:
    sc = engine.scenario
    objs = sc.objectives
    total = len(objs)
    met = sum(1 for o in objs if engine.state.objective_status.get(o.id) == "Achieved")

    outcome = engine.state.outcome or ("WIN" if engine.objectives_met else "INCOMPLETE")

    # Score: objective coverage is the bulk; deny OPFOR for the win bonus; finishing
    # inside the clock earns a tempo bonus. A LOSS caps in the F range regardless of
    # coverage — the adversary achieved their aim.
    coverage = (met / total) if total else 0.0
    denied = not engine.opfor_won
    made_window = engine.minutes_left > 0
    if outcome == "LOSS":
        score = round(coverage * 30)  # partial credit only
    else:
        score = round(
            coverage * 60
            + (25 if denied and outcome == "WIN" else 0)
            + (15 if made_window and outcome == "WIN" else 0)
        )
    score = max(0, min(100, score))

    highlights: list[str] = []
    for o in objs:
        done = engine.state.objective_status.get(o.id) == "Achieved"
        failed = engine.state.objective_status.get(o.id) == "Failed"
        mark = "x" if done else "!" if failed else " "
        highlights.append(f"[{mark}] {o.name}")

    if sc.opfor.win_threshold > 0:
        highlights.append(
            f"{sc.opfor.name or 'OPFOR'} progress: "
            f"{engine.state.opfor_progress}/{sc.opfor.win_threshold}"
            + (" — objective achieved." if engine.opfor_won else " — denied.")
        )
    highlights.append(
        f"Closed in {engine.state.clock_minutes}m of the {engine.window_minutes}m window."
    )

    # Schlussbesprechung: how the umpire read the player's decisions.
    for r in engine.state.rulings[-4:]:
        highlights.append(f"T{r.turn} [{r.band}→{r.tier}] {r.action}: {r.critique}")

    return Aar(
        outcome=outcome,
        grade=_grade(score),
        score=score,
        objectives_met=met,
        objectives_total=total,
        minutes_spent=engine.state.clock_minutes,
        window_minutes=engine.window_minutes,
        opfor_progress=engine.state.opfor_progress,
        opfor_threshold=sc.opfor.win_threshold,
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
