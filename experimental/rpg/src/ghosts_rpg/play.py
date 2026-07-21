"""Terminal play loop — play the game right now, no UI needed.

    python -m ghosts_rpg.play                  # plays the bundled phishing-drill
    python -m ghosts_rpg.play path/to/bundle.json

Green-on-black Zork feel. Computer turns are narrated; at your turn one or more
tickets are open at once — clear them in any order before lunch. Address a ticket
with 'task N: <action>' (e.g. 'task 4: isolate the host'), or free text when only
one is open. Each action burns minutes off your morning.
"""

from __future__ import annotations

import sys
from pathlib import Path

from .game import Beat, Frame, Game
from .llm import OllamaClient
from .loader import load_bundle_file

_DIM = "\033[2m"
_GREEN = "\033[32m"
_CYAN = "\033[36m"
_YELLOW = "\033[33m"
_BOLD = "\033[1m"
_RESET = "\033[0m"

_CELL_COLOR = {"Red Team": _YELLOW, "Blue Team": _CYAN, "White Cell": _DIM, "Green Cell": _GREEN}


def _default_fixture() -> Path:
    return Path(__file__).resolve().parents[2] / "fixtures" / "scenarios" / "phishing-drill.json"


def _render(frame: Frame) -> None:
    for b in frame.beats:
        color = _CELL_COLOR.get(b.cell, _RESET)
        print(f"{color}{_BOLD}[{b.time} · {b.cell}]{_RESET}")
        print(f"{color}{b.text}{_RESET}\n")
    for n in frame.notices:
        print(f"{_DIM}» {n}{_RESET}")
    if frame.tasks:
        left = frame.hud.get("minutesLeft")
        budget = frame.hud.get("lunchMinutes")
        queued = frame.hud.get("queuedTasks", 0)
        print(f"\n{_YELLOW}⏱  {left}m to lunch (of {budget}m) · {len(frame.tasks)} ticket(s) in front of you{_RESET}")
        for t in frame.tasks:
            acts = "   ".join(f"{_BOLD}{a['label']}{_RESET}" for a in t["actions"])
            print(f"{_CYAN}  [task {t['step']} · {t['time']}]{_RESET} {acts}")
        print(f"{_DIM}  address one with 'task N: <action>'{_RESET}")
        if frame.can_table:
            print(f"{_DIM}  or 'next' to pull up the next ticket ({queued} more waiting){_RESET}")


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
    game = Game(bundle, llm=OllamaClient())

    print(f"{_GREEN}{_BOLD}{bundle.scenario.name}{_RESET}")
    print(f"{_DIM}{bundle.scenario.description}{_RESET}\n")
    if not game.dm.llm.enabled:
        print(f"{_DIM}(DM offline fallback — set OLLAMA_MODEL for richer narration){_RESET}\n")

    frame = game.start()
    _render(frame)

    while not frame.is_complete:
        try:
            raw = input(f"\n{_GREEN}> {_RESET}").strip()
        except (EOFError, KeyboardInterrupt):
            print()
            return 0
        if raw.lower() in {"quit", "exit"}:
            return 0
        frame = game.act(raw)
        print()
        _render(frame)

    _render_aar(frame)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
