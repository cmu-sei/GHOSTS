"""Exercise-control adjudication for staff-product submissions."""

from pathlib import Path

from ghosts_rpg.control import Leitung
from ghosts_rpg.dm import DungeonMaster
from ghosts_rpg.engine import Engine
from ghosts_rpg.game import Game
from ghosts_rpg.loader import load_bundle_file

FIXTURE = Path(__file__).resolve().parents[1] / "fixtures" / "scenarios" / "soc-morning.json"


def test_staff_product_parser_extracts_sections():
    engine = Engine(load_bundle_file(FIXTURE))
    control = Leitung(DungeonMaster(engine))

    product = control.parse_staff_product(
        """Priority: ransomware precursor
Plan: isolate FIN-WS-04 and kill the shadow-copy deletion
Assumptions: EDR isolation works; SOC has authority
Information Requests: confirm SMB sessions to FS01
Risk: VPN lockout waits"""
    )

    assert product.is_structured
    assert product.priority == "ransomware precursor"
    assert "isolate FIN-WS-04" in product.plan
    assert product.assumptions == ["EDR isolation works", "SOC has authority"]
    assert product.information_requests == ["confirm SMB sessions to FS01"]
    assert product.risk_acceptance == "VPN lockout waits"


def test_staff_order_on_decisive_ticket_sets_flag_and_records_findings():
    game = Game(load_bundle_file(FIXTURE))
    game.start()
    game.act("next")  # reveal ticket 4

    frame = game.act(
        """task 4: Priority: ransomware precursor
Plan: isolate FIN-WS-04 and kill the shadow-copy deletion
Assumptions: EDR isolation works; SOC has authority
Information Requests: confirm SMB sessions to FS01"""
    )

    assert "ransomware-contained" in game.engine.state.flags
    assert "EDR isolation works" in game.engine.state.assumptions
    findings = " ".join(frame.notices + game.engine.state.umpire_findings)
    assert "staff product" in findings
    assert "decisive containment constraint" in findings
    assert frame.hud["umpireFindings"]
