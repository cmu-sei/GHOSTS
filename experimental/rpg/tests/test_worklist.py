"""Worklist mode: sequential ticket reveal ('table' to stack), per-task flag
ownership, and the lunch clock.

Exercises the SOC-morning fixture, where steps 3/4/5 are all Blue-Team tickets but
arrive ONE AT A TIME — only ticket 3 is shown at start; the player tables ('next')
to surface 4 and 5, or resolving a ticket auto-flows to the next. Containing ticket
#4 (ransomware) is the only decisive move that steers the branch."""

from pathlib import Path

import pytest

from ghosts_rpg.game import Game
from ghosts_rpg.loader import load_bundle_file

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "soc-morning.json"


@pytest.fixture
def game():
    return Game(load_bundle_file(FIXTURE))  # llm=None -> offline DM


def _steps(frame):
    return {t["step"] for t in frame.tasks}


def test_only_first_ticket_shown_at_start(game):
    frame = game.start()
    assert frame.awaiting_player
    # Tickets arrive one at a time: only the first is on the board, though all three
    # are open behind the scenes (queued).
    assert _steps(frame) == {3}
    assert frame.tasks[0]["actions"]  # it carries its own action menu
    assert frame.hud["openTasks"] == 3
    assert frame.hud["shownTasks"] == 1
    assert frame.hud["queuedTasks"] == 2
    assert frame.can_table


def test_table_stacks_up_to_all_three(game):
    game.start()
    frame = game.act("next")  # table ticket 3, pull up 4
    assert _steps(frame) == {3, 4}
    assert any(b.step_number == 4 for b in frame.beats)
    frame = game.act("next")  # pull up 5 — now all three sit open
    assert _steps(frame) == {3, 4, 5}
    assert frame.hud["queuedTasks"] == 0
    assert not frame.can_table
    frame = game.act("next")  # nothing left to reveal
    assert any("already on your board" in n.lower() for n in frame.notices)


def test_resolving_auto_flows_to_next_ticket(game):
    game.start()  # ticket 3 shown
    # Resolving a ticket auto-surfaces the next queued one, so the queue flows
    # sequentially without the player tabling every time.
    frame = game.act("notify the team")  # resolves 3 -> reveals 4
    assert _steps(frame) == {4}
    assert any(b.step_number == 4 for b in frame.beats)
    frame = game.act("kill the process and isolate the host")  # resolves 4 -> reveals 5
    assert _steps(frame) == {5}
    frame = game.act("reset the account")  # resolves 5 -> queue empty
    # Worklist emptied -> engine advanced into the Red Team branch.
    assert any(b.step_number in (6, 7) for b in frame.beats)


def test_only_ransomware_task_owns_its_flag(game):
    game.start()
    game.act("next")  # reveal 4
    game.act("next")  # reveal 5 — all three now on the board
    # Resolving the VPN ticket must NOT set the ransomware branch flag.
    game.act("task 5: reset the account")
    assert "ransomware-contained" not in game.engine.state.flags
    # Resolving the ransomware ticket does.
    game.act("task 4: isolate the host and kill the process")
    assert "ransomware-contained" in game.engine.state.flags


def test_contain_ransomware_wins(game):
    game.start()  # ticket 3 shown
    # Table past the phishing ticket to reach the ransomware ticket and handle it
    # first — inside the 30m fuse — then clear the rest.
    game.act("next")  # reveal 4 (ransomware); 3 still open behind it
    game.act("task 4: isolate FIN-WS-04 and kill the deletion")  # resolves 4 -> reveals 5
    game.act("task 3: notify the team")
    frame = game.act("task 5: notify the team")
    # Contained branch (step 6) fires, not the encryption branch (step 7).
    assert any(b.step_number == 6 for b in frame.beats)
    assert not any(b.step_number == 7 for b in frame.beats)
    frame = game.act("stand the team down")
    assert frame.is_complete
    assert frame.aar["outcome"] == "WIN"


def test_ignore_ransomware_loses(game):
    game.start()
    # Defer every ticket, including the ransomware one -> no containment flag.
    game.act("task 3: defer for now")
    game.act("task 4: defer for now")
    frame = game.act("task 5: defer for now")
    assert any(b.step_number == 7 for b in frame.beats)  # encryption spreads
    assert not any(b.step_number == 6 for b in frame.beats)
    frame = game.act("isolate the host")
    assert frame.is_complete
    assert frame.aar["outcome"] == "LOSS"


def test_lunch_clock_counts_down(game):
    frame = game.start()
    assert frame.hud["lunchMinutes"] == 45  # durationHours 0.75
    assert frame.hud["containmentFuseMinutes"] == 30
    assert frame.hud["containmentFuseMinutesLeft"] == 30
    assert frame.hud["containmentContained"] is False
    before = frame.hud["minutesLeft"]
    game.act("next")  # reveal ticket 4
    frame = game.act("task 4: isolate the host")  # a decisive action costs the most
    assert frame.hud["minutesLeft"] < before
    assert frame.hud["containmentContained"] is True
    assert frame.hud["containmentFuseMinutesLeft"] is None


def test_made_lunch_reflected_in_aar(game):
    game.start()
    # Contain the ransomware first (inside the fuse), then clear the noise cheaply
    # with notify so the whole morning stays under the 45m budget.
    game.act("next")  # reveal ticket 4
    game.act("task 4: isolate the host and kill the process")  # 15m
    game.act("task 3: notify the team")  # 5m
    game.act("task 5: notify the team")  # 5m
    frame = game.act("stand the team down")
    assert frame.is_complete
    assert frame.aar["outcome"] == "WIN"
    assert frame.aar["made_lunch"] is True
    assert frame.aar["minutes_spent"] <= frame.aar["lunch_minutes"]


def test_soft_action_on_single_shown_ticket_is_unambiguous(game):
    game.start()  # only ticket 3 shown -> no disambiguation needed
    frame = game.act("investigate")
    assert frame.awaiting_player
    assert _steps(frame) == {3}  # recon leaves the ticket open, still the only one shown
    assert not any("which one" in n.lower() for n in frame.notices)


def test_disambiguation_needed_once_several_are_stacked(game):
    game.start()
    game.act("next")  # reveal 4
    game.act("next")  # reveal 5 — now three tickets share the board
    # "investigate" is a soft action every shown ticket recognizes -> ambiguous.
    frame = game.act("investigate")
    assert frame.awaiting_player
    assert _steps(frame) == {3, 4, 5}  # nothing resolved
    assert any("which one" in n.lower() for n in frame.notices)


def test_blind_clicking_noise_first_detonates(game):
    game.start()  # ticket 3 (phishing) shown
    # Waste the budget on the two noise tickets with decisive actions (15m each = 30m,
    # hitting the fuse), tabling past the ransomware ticket instead of handling it.
    game.act("task 3: quarantine the email")  # 15m, resolves 3 -> reveals 4 (ransomware)
    game.act("next")  # table the ransomware ticket, pull up 5 (VPN)
    frame = game.act("task 5: block the account and force a reset")  # 15m -> clock at 30m
    assert game.engine.state.detonated
    assert any(b.step_number == 7 for b in frame.beats)  # encryption branch fired
    assert not any(b.step_number == 6 for b in frame.beats)
    assert any("detonat" in n.lower() for n in frame.notices)


def test_repeating_generic_containment_orders_does_not_win(game):
    game.start()
    game.act("issue containment order")  # generic order on ticket 3 -> reveals 4
    frame = game.act("issue containment order")  # generic order on decisive ticket

    assert game.engine.state.detonated
    assert "ransomware-contained" not in game.engine.state.flags
    assert any(b.step_number == 7 for b in frame.beats)
    assert any("generic" in n.lower() for n in frame.notices)


def test_detonation_abandons_the_ransomware_ticket(game):
    game.start()
    # Blow the fuse having never handled the ransomware ticket. Detonation abandons
    # every still-open Blue-Team ticket, so the ransomware ticket (#4) is gone and its
    # flag was never set.
    game.act("task 3: quarantine the email")  # 15m -> reveals 4
    game.act("next")  # table 4, reveal 5
    frame = game.act("task 5: block the account and force a reset")  # 30m -> detonated
    assert game.engine.state.detonated
    assert 4 not in _steps(frame)  # the ransomware ticket is no longer open
    assert "ransomware-contained" not in game.engine.state.flags


def test_detonation_is_hard_loss(game):
    game.start()
    game.act("task 3: quarantine the email")
    game.act("next")
    game.act("task 5: block the account and force a reset")  # detonated at 30m
    frame = game.act("stand the team down")
    assert frame.is_complete
    assert frame.aar["outcome"] == "LOSS"
    assert frame.aar["grade"] == "F"
    assert frame.aar["made_lunch"] is False


def test_investigating_ransomware_reveals_the_fuse(game):
    game.start()
    game.act("next")  # reveal the ransomware ticket (4)
    game.act("task 4: investigate")  # recon on it
    intel = " ".join(game.engine.state.knowledge).lower()
    assert "detonate" in intel and "30m" in intel
