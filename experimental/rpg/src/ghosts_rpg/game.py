"""Game orchestration — the worklist turn loop.

Drives engine + DM: auto-plays computer-owned steps (Red/White/Green), stops at a
Blue Team *worklist* (one or more open tasks the player clears in any order, each
burning minutes off the lunch clock), applies validated player proposals, and lets
the engine pick the forward branch once the worklist is empty. Returns a structured
frame the UI (terminal or Angular) renders.
"""

from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Optional

from .control import Leitung
from .dm import DungeonMaster, Task
from .engine import Engine
from .models import ScenarioBundle, TranscriptEntry
from .scoring import Aar, review


@dataclass
class Beat:
    """One narrated event (a computer turn the DM played, or the player's situation)."""

    step_number: int
    cell: str
    time: str
    text: str


@dataclass
class Frame:
    """What changed since the last interaction, plus the current worklist."""

    beats: list[Beat] = field(default_factory=list)
    tasks: list[dict] = field(default_factory=list)  # revealed worklist tasks + their action menus
    awaiting_player: bool = False
    notices: list[str] = field(default_factory=list)  # "No effect: ..." etc.
    is_complete: bool = False
    aar: Optional[dict] = None
    hud: dict = field(default_factory=dict)
    can_table: bool = False  # an un-revealed ticket is queued behind the current one


class Game:
    def __init__(self, bundle: ScenarioBundle, llm=None):
        self.bundle = bundle
        self.engine = Engine(bundle)
        self.dm = DungeonMaster(self.engine, llm=llm)
        self.control = Leitung(self.dm)

    # ── lifecycle ────────────────────────────────────────────────────────

    def start(self) -> Frame:
        self.engine.start()
        return self._play_to_worklist()

    # Phrases that mean "set this ticket aside and show me the next one" — table it.
    _TABLE = ("table it", "table this", "table", "next ticket", "next", "set aside",
              "come back", "another ticket", "show another", "queue")

    def act(self, player_input: str) -> Frame:
        if not self.engine.open_worklist():
            return self._frame(notices=["The exercise is not waiting on you right now."])
        # The player acts only on tickets currently surfaced to them.
        tasks = self.engine.revealed_worklist()

        # "Table it": reveal the next queued ticket without clearing the current one,
        # so the player can stack tickets open. Costs no time — it's just triage.
        if self._is_table_command(player_input):
            revealed = self.engine.reveal_next()
            if revealed is None:
                return self._frame(awaiting=True, notices=["Every open ticket is already on your board."])
            beat = self._reveal_beat(revealed)
            return self._frame(beats=[beat], awaiting=True,
                               notices=["Ticket set aside — pulling up the next one."])

        target, cleaned = self._resolve_target(player_input, tasks)
        if target is None:
            if len(tasks) == 1:
                # They named a queued ticket that isn't on the board yet.
                notice = ("That ticket isn't in front of you yet. Say 'next' to pull "
                          "up the next one, or act on the current ticket.")
            else:
                notice = (f"You have {len(tasks)} tickets in front of you — say which one "
                          f"(e.g. 'task {tasks[0].number}: investigate').")
            return self._frame(awaiting=True, notices=[notice])

        ruling = self.control.adjudicate(cleaned, target)
        proposal = ruling.proposal
        self.engine.state.transcript.append(
            TranscriptEntry(step_number=target.number, cell=target.assigned, speaker="Player", text=player_input)
        )
        result = self.engine.apply(proposal)
        self.engine.spend_time(proposal.minutes)
        notices = list(ruling.findings) + list(result.messages)
        # Only flag truly unrecognized input (a no-op that neither acts nor resolves
        # the task). "wait" is a legitimate choice that resolves a task with no effects.
        if not proposal.effects and not proposal.resolves_task:
            notices.append(f'No effect: "{player_input.strip()}" did nothing here. Try investigate, handle, notify, or defer.')

        beats: list[Beat] = []
        if proposal.resolves_task:
            self.engine.clear_task(target.number)
            self.engine.state.revealed_steps.discard(target.number)
            # Resolving a ticket flows to the next queued one automatically: seed the
            # revealed set (surfaces the next open ticket if none is showing) and
            # narrate whatever just came onto the board.
            before = set(self.engine.state.revealed_steps)
            for e in self.engine.revealed_worklist():
                if e.number not in before:
                    beats.append(self._reveal_beat(e))

        # The fuse takes priority: if the clock just passed the containment deadline
        # with the threat still un-contained, it detonates now — abandoning any tasks
        # still open — and the timeline jumps onto the loss branch.
        if self.engine.fuse_blown():
            return self._detonate(notices=notices)

        # The lunch clock is a hard ceiling: if it's exhausted, the morning ends here
        # regardless of what's still open.
        if self.engine.out_of_time():
            self.engine.state.is_complete = True
            return self._frame(notices=notices + ["Out of time — the morning is over."], complete=True)

        if proposal.resolves_task and not self.engine.open_worklist():
            self.engine.advance_past_worklist()
            return self._play_to_worklist(notices=notices)
        # Task still open (recon / no-op), or other tasks remain: re-present worklist,
        # including any newly surfaced ticket from resolving the last one.
        return self._frame(beats=beats, awaiting=True, notices=notices)

    def _detonate(self, notices: list[str]) -> Frame:
        """The containment fuse blew: narrate the detonation, then play the forced
        loss branch through to the next worklist (or the end)."""
        event = self.engine.detonate()
        notices = notices + ["The clock ran out on the threat — it detonated before you contained it."]
        if event is None:
            return self._frame(notices=notices, complete=True)
        return self._play_to_worklist(notices=notices)

    def _is_table_command(self, player_input: str) -> bool:
        """True when the input means 'table this / show me the next ticket'. Matches a
        table phrase as a whole word/phrase so it can't hijack a real action (e.g.
        'remove', 'restore') that merely contains one of the fragments."""
        import re

        text = player_input.strip().lower()
        return any(re.search(rf"\b{re.escape(k)}\b", text) for k in self._TABLE)

    def _reveal_beat(self, event) -> Beat:
        """Narrate a newly surfaced ticket into the transcript and return its beat."""
        text = self.dm.narrate(event)
        self.engine.state.transcript.append(
            TranscriptEntry(step_number=event.number, cell="Blue Team", speaker="DM", text=text)
        )
        return Beat(event.number, "Blue Team", event.time, text)

    def _resolve_target(self, player_input: str, tasks) -> tuple[Optional[object], str]:
        """Pick which revealed task the input targets, and strip any 'task N:' prefix.

        A single revealed task needs no disambiguation. With several revealed, the
        input must name one by number ('task 7 ...' / '#7 ...' / a leading '7'); if
        it does not, we also accept it when only one task offers a matching action."""
        by_num = {e.number: e for e in tasks}
        num, cleaned = self._extract_task_number(player_input)

        if len(tasks) == 1:
            # A number naming a ticket that's real but not yet on the board means the
            # player is reaching for a queued ticket — tell them to table to it.
            if num is not None and num != tasks[0].number and self.engine._by_num.get(num):
                return None, player_input
            return tasks[0], cleaned if num == tasks[0].number else player_input

        if num is not None and num in by_num:
            return by_num[num], cleaned

        # No explicit number: accept only if exactly one task recognizes the action.
        matches = [e for e in tasks if self.dm.interpret(player_input, e).resolves_task
                   or self.dm.interpret(player_input, e).effects]
        if len(matches) == 1:
            return matches[0], player_input
        return None, player_input

    @staticmethod
    def _extract_task_number(text: str) -> tuple[Optional[int], str]:
        """Parse a leading task selector: 'task 7: x' / '#7 x' / '7 x' -> (7, 'x')."""
        import re

        m = re.match(r"\s*(?:task\s*|#)?(\d+)\s*[:.\-)]?\s*(.*)", text, re.IGNORECASE | re.DOTALL)
        if not m:
            return None, text
        num = int(m.group(1))
        rest = m.group(2).strip()
        return num, (rest or text)

    # ── internal turn loop ─────────────────────────────────────────────

    def _play_to_worklist(self, notices: Optional[list[str]] = None) -> Frame:
        """Narrate every computer-owned step until a player worklist opens or the end."""
        beats: list[Beat] = []
        while True:
            event = self.engine.current_event()
            if event is None:  # exhausted -> complete
                break
            if event.is_player_turn:
                # Surface the revealed ticket(s) — one at a time by default — into the
                # transcript, then present. More arrive as the player tables/resolves.
                turn = self.dm.worklist()
                for t in turn.tasks:
                    beats.append(Beat(t.step_number, "Blue Team", t.time, t.prompt))
                    self.engine.state.transcript.append(
                        TranscriptEntry(step_number=t.step_number, cell="Blue Team", speaker="DM", text=t.prompt)
                    )
                return self._frame(beats=beats, awaiting=True, notices=notices)
            text = self.dm.narrate(event)
            beats.append(Beat(event.number, event.assigned, event.time, text))
            self.engine.state.transcript.append(
                TranscriptEntry(step_number=event.number, cell=event.assigned, speaker="DM", text=text)
            )
            self.engine.advance()
        return self._frame(beats=beats, notices=notices, complete=True)

    # ── frame assembly ──────────────────────────────────────────────────

    def _frame(self, beats=None, awaiting=False, notices=None, complete=False) -> Frame:
        complete = complete or self.engine.state.is_complete
        aar: Aar | None = review(self.engine) if complete else None
        return Frame(
            beats=beats or [],
            tasks=self._worklist_dicts() if awaiting else [],
            awaiting_player=awaiting,
            notices=notices or [],
            is_complete=complete,
            aar=asdict(aar) if aar else None,
            hud=self.hud(),
            can_table=awaiting and self.engine.has_unrevealed_tasks(),
        )

    def _worklist_dicts(self) -> list[dict]:
        return [
            {
                "step": t.step_number,
                "time": t.time,
                "prompt": t.prompt,
                "actions": [{"label": a.label, "intent": a.intent} for a in t.actions],
            }
            for t in self.dm.worklist().tasks
        ]

    def hud(self) -> dict:
        sc = self.bundle.scenario
        st = self.engine.state
        open_tasks = self.engine.open_worklist()
        revealed = self.engine.revealed_worklist() if open_tasks else []
        objs = [
            {
                "id": o.id,
                "name": o.name,
                "met": st.objective_status.get(o.id) == "Achieved",
            }
            for o in self.bundle.objectives
        ]
        actors = sc.scenario_parameters.threat_actors if sc.scenario_parameters else []
        return {
            "scenario": sc.name,
            "step": open_tasks[0].number if open_tasks else None,
            "time": open_tasks[0].time if open_tasks else None,
            "role": "Blue Team (SOC Analyst)",
            "objectives": objs,
            "flags": sorted(st.flags),
            "knowledge": list(st.knowledge),
            "umpireFindings": list(st.umpire_findings[-6:]),
            "assumptions": list(st.assumptions),
            "constraints": self._constraints(),
            "threats": [a.name for a in actors],
            "openTasks": len(open_tasks),
            "shownTasks": len(revealed),
            "queuedTasks": len(open_tasks) - len(revealed),
            "minutesLeft": self.engine.minutes_left,
            "lunchMinutes": self.engine.lunch_minutes,
            "containmentFuseMinutes": self.engine.containment_deadline,
            "containmentFuseMinutesLeft": self._containment_fuse_minutes_left(),
            "containmentContained": self.engine.is_contained,
        }

    def _constraints(self) -> list[str]:
        constraints: list[str] = []
        if self.engine.containment_deadline is not None:
            constraints.append(f"Containment fuse: {self.engine.containment_deadline}m")
        if self.engine.known_flags:
            constraints.append("Steering flags: " + ", ".join(sorted(self.engine.known_flags)))
        constraints.append(f"Exercise window: {self.engine.lunch_minutes}m")
        return constraints

    def _containment_fuse_minutes_left(self) -> Optional[int]:
        deadline = self.engine.containment_deadline
        if deadline is None or self.engine.is_contained or self.engine.state.detonated:
            return None
        return max(0, deadline - self.engine.state.minutes_spent)
