"""Exercise control for free-Kriegsspiel style adjudication.

The Leitung receives the player's staff product, checks it against the scenario's
known constraints, and returns findings plus the proposal the engine should apply.
The engine remains authoritative for state changes; this layer explains why the
order was or was not good enough.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import re

from .dm import DungeonMaster
from .engine import Proposal
from .models import StaffProduct, TimelineEvent


@dataclass
class ControlResult:
    proposal: Proposal
    product: StaffProduct
    findings: list[str] = field(default_factory=list)


class Leitung:
    """Umpired exercise-control cell.

    This is deliberately deterministic. The LLM may narrate, but it does not decide
    whether a plan beats time, access, or containment constraints.
    """

    _SECTION_NAMES = {
        "priority": "priority",
        "intent": "intent",
        "commander's intent": "intent",
        "commander intent": "intent",
        "plan": "plan",
        "order": "plan",
        "actions": "plan",
        "assumption": "assumptions",
        "assumptions": "assumptions",
        "information request": "information_requests",
        "information requests": "information_requests",
        "pir": "information_requests",
        "irs": "information_requests",
        "risk": "risk_acceptance",
        "risk acceptance": "risk_acceptance",
    }

    _CONTAINMENT = (
        "contain",
        "isolate",
        "quarantine",
        "kill",
        "block",
        "disable",
        "reset",
        "disconnect",
        "remediate",
    )
    _RECON = ("investigate", "triage", "scope", "confirm", "review", "estimate")
    _SPECIFIC_CONTAINMENT = (
        "quarantine",
        "block",
        "isolate",
        "kill",
        "disable",
        "reset",
        "disconnect",
        "remediate",
        "remove",
        "revoke",
        "lock",
        "force",
        "shadow-copy",
        "domain",
        "email",
        "inbox",
        "host",
        "process",
        "account",
        "fin-ws-04",
    )

    def __init__(self, dm: DungeonMaster):
        self.dm = dm
        self.engine = dm.engine

    def adjudicate(self, player_input: str, event: TimelineEvent) -> ControlResult:
        product = self.parse_staff_product(player_input)
        proposal = self.dm.interpret(player_input, event)
        if self._is_generic_decisive_containment(product, event, proposal):
            proposal.effects = []
        findings = self._findings(product, event, proposal)
        self._record(product, findings)
        return ControlResult(proposal=proposal, product=product, findings=findings)

    def parse_staff_product(self, raw_text: str) -> StaffProduct:
        text = raw_text.strip()
        fields: dict[str, str | list[str] | bool] = {
            "raw_text": text,
            "priority": "",
            "intent": "",
            "plan": "",
            "assumptions": [],
            "information_requests": [],
            "risk_acceptance": "",
            "is_structured": False,
        }
        current: str | None = None
        for raw_line in text.splitlines():
            line = raw_line.strip()
            if not line:
                continue
            key, value = self._split_section(line)
            if key:
                current = key
                fields["is_structured"] = True
                self._append(fields, current, value)
            elif current:
                self._append(fields, current, line)

        if not fields["is_structured"]:
            lowered = text.lower()
            if any(k in lowered for k in self._CONTAINMENT):
                fields["plan"] = text
            elif any(k in lowered for k in self._RECON):
                fields["intent"] = text
            else:
                fields["plan"] = text

        return StaffProduct(**fields)

    def _split_section(self, line: str) -> tuple[str | None, str]:
        match = re.match(r"^([A-Za-z][A-Za-z '\-/]{1,32})\s*:\s*(.*)$", line)
        if not match:
            return None, line
        name = match.group(1).strip().lower()
        key = self._SECTION_NAMES.get(name)
        if not key:
            return None, line
        return key, match.group(2).strip()

    @staticmethod
    def _append(fields: dict[str, str | list[str] | bool], key: str, value: str) -> None:
        if not value:
            return
        if key in {"assumptions", "information_requests"}:
            items = fields[key]
            assert isinstance(items, list)
            items.extend(_split_items(value))
            return
        current = str(fields[key])
        fields[key] = f"{current} {value}".strip() if current else value

    def _findings(
        self, product: StaffProduct, event: TimelineEvent, proposal: Proposal
    ) -> list[str]:
        findings: list[str] = []
        task_text = event.description.lower()
        order_text = " ".join(
            [
                product.priority,
                product.intent,
                product.plan,
                " ".join(product.assumptions),
                " ".join(product.information_requests),
                product.risk_acceptance,
            ]
        ).lower()

        if product.is_structured:
            findings.append("Leitung received a staff product, not just a move.")
        else:
            findings.append("Leitung treated this as a hasty oral order.")

        if product.assumptions:
            findings.append(
                "Assumptions logged for critique: " + "; ".join(product.assumptions[:3])
            )

        owns_branch = bool(self.engine.flags_owned_by(event))
        if owns_branch:
            deadline = self.engine.containment_deadline
            if deadline is not None:
                findings.append(
                    f"Time-distance check: this task must be contained before the {deadline}m fuse."
                )
            if any(k in order_text for k in self._CONTAINMENT) and not self._has_specific_containment_detail(product):
                findings.append(
                    "Umpire ruling: generic containment order lacks concrete tasking; the decisive threat keeps running."
                )
            elif any(k in order_text for k in self._CONTAINMENT):
                findings.append("Umpire ruling: order addresses the decisive containment constraint.")
            else:
                findings.append(
                    "Umpire ruling: order does not contain the decisive threat; the clock keeps running."
                )
        elif "ransomware" in task_text or "shadow-copy" in task_text:
            findings.append("Umpire note: this ticket looks decisive, but owns no branch flag in the data.")
        elif proposal.resolves_task and any(k in order_text for k in self._CONTAINMENT):
            findings.append("Umpire ruling: action resolves local work but does not affect the main branch.")

        if product.information_requests:
            findings.append(
                "Recon requests queued against the player picture: "
                + "; ".join(product.information_requests[:3])
            )

        if not proposal.effects and not proposal.resolves_task:
            findings.append("No adjudicated effect: the product lacked an executable tasking.")
        return findings

    def _is_generic_decisive_containment(
        self, product: StaffProduct, event: TimelineEvent, proposal: Proposal
    ) -> bool:
        return (
            proposal.resolves_task
            and bool(proposal.effects)
            and bool(self.engine.flags_owned_by(event))
            and not self._has_specific_containment_detail(product)
        )

    def _has_specific_containment_detail(self, product: StaffProduct) -> bool:
        order_text = " ".join(
            [
                product.priority,
                product.intent,
                product.plan,
                " ".join(product.assumptions),
                " ".join(product.information_requests),
                product.risk_acceptance,
            ]
        ).lower()
        return any(k in order_text for k in self._SPECIFIC_CONTAINMENT)

    def _record(self, product: StaffProduct, findings: list[str]) -> None:
        state = self.engine.state
        for assumption in product.assumptions:
            if assumption not in state.assumptions:
                state.assumptions.append(assumption)
        for finding in findings:
            if finding not in state.umpire_findings:
                state.umpire_findings.append(finding)


def _split_items(value: str) -> list[str]:
    cleaned = value.strip().lstrip("-*").strip()
    if not cleaned:
        return []
    parts = [p.strip(" -*") for p in re.split(r"\s*;\s*", cleaned) if p.strip(" -*")]
    return parts or [cleaned]
