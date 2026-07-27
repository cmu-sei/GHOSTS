"""Terminal play loop — Kriegspiel Mode, no UI needed.

    python -m ghosts_rpg.play                       # plays meridian-hybrid
    python -m ghosts_rpg.play path/to/scenario.json

Green-on-black feel. Exercise control sets the situation; you are the incident
commander. Each turn, state an action and (optionally) your rationale — the umpire
weighs your reasoning into odds, rolls hidden, and narrates the consequence. The
adversary reacts. Type your action, then your rationale when prompted (blank to
skip). 'quit' to leave.
"""

from __future__ import annotations

import sys
from pathlib import Path

from .game import Frame, Game
from .llm import make_llm
from .loader import load_bundle_file

_DIM = "\033[2m"
_GREEN = "\033[32m"
_CYAN = "\033[36m"
_YELLOW = "\033[33m"
_RED = "\033[31m"
_BOLD = "\033[1m"
_RESET = "\033[0m"

_SPEAKER_COLOR = {"Signals": _YELLOW, "Judge": _CYAN, "Control": _DIM, "Player": _GREEN}


def _default_fixture() -> Path:
    return Path(__file__).resolve().parents[2] / "fixtures" / "scenarios" / "meridian-hybrid.json"


def _render(frame: Frame) -> None:
    for b in frame.beats:
        color = _SPEAKER_COLOR.get(b.speaker, _RESET)
        print(f"{color}{_BOLD}[{b.speaker}]{_RESET}")
        print(f"{color}{b.text}{_RESET}\n")
    if frame.band:
        print(f"{_YELLOW}» Umpire odds: {_BOLD}{frame.band}{_RESET}{_YELLOW} → {frame.tier}{_RESET}")
        if frame.critique:
            print(f"{_DIM}  {frame.critique}{_RESET}")
    for n in frame.notices:
        print(f"{_DIM}» {n}{_RESET}")
    if frame.awaiting_player:
        h = frame.hud
        left = h.get("minutesLeft")
        window = h.get("windowMinutes")
        label = h.get("clockLabel", "window")
        prog = h.get("opforProgress")
        thresh = h.get("opforThreshold")
        print(f"\n{_YELLOW}⏱  {left}m {label} (of {window}m) · "
              f"{h.get('opforName', 'OPFOR')} {prog}/{thresh}{_RESET}")
        indicators = h.get("indicators", [])
        if indicators:
            print(f"{_DIM}  picture: {indicators[-1]}{_RESET}")
        print(f"{_DIM}  state an action; you'll be asked for your rationale{_RESET}")


def _render_aar(frame: Frame) -> None:
    aar = frame.aar or {}
    print(f"\n{_BOLD}══════ AFTER-ACTION REVIEW ══════{_RESET}")
    print(f"Outcome: {_BOLD}{aar.get('outcome')}{_RESET}   "
          f"Grade: {_BOLD}{aar.get('grade')}{_RESET}   "
          f"Score: {aar.get('score')}/100   "
          f"Objectives: {aar.get('objectives_met')}/{aar.get('objectives_total')}")
    for h in aar.get("highlights", []):
        print(f"  {h}")
    print(f"{_BOLD}════════════════════════════════{_RESET}")


def main(argv: list[str] | None = None) -> int:
    argv = argv if argv is not None else sys.argv[1:]
    path = Path(argv[0]) if argv else _default_fixture()
    bundle = load_bundle_file(path)
    game = Game(bundle, llm=make_llm())

    print(f"{_GREEN}{_BOLD}{bundle.scenario.name}{_RESET}")
    print(f"{_DIM}{bundle.scenario.description}{_RESET}\n")
    if not game.llm.enabled:
        print(f"{_DIM}(offline stub — set OLLAMA_MODEL or RPG_LLM_PROVIDER=bedrock for the real judge/OPFOR){_RESET}\n")

    frame = game.start()
    _render(frame)

    while not frame.is_complete:
        try:
            action = input(f"\n{_GREEN}action> {_RESET}").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return 0
        if action.lower() in {"quit", "exit"}:
            return 0
        if not action:
            continue
        try:
            rationale = input(f"{_GREEN}because> {_RESET}").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return 0
        frame = game.act(action, rationale)
        print()
        _render(frame)

    _render_aar(frame)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
