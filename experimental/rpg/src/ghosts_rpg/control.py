"""Exercise control for free-Kriegsspiel style adjudication.

The Leitung receives the player's staff product, checks it against the scenario's
known constraints, and returns findings plus the proposal the engine should apply.
The engine remains authoritative for state changes; this layer explains why the
order was or was not good enough.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import json
import re

from .dm import DungeonMaster
from .engine import Effect, EffectKind, Proposal
from .models import StaffProduct, TimelineEvent


@dataclass
class ControlResult:
    proposal: Proposal
    product: StaffProduct
    findings: list[str] = field(default_factory=list)


@dataclass(frozen=True)
class StaffAssessment:
    finding: str
    knowledge: str
    complete_objectives: tuple[int, ...] = ()


@dataclass(frozen=True)
class JudgeRuling:
    findings: tuple[str, ...]
    knowledge: tuple[str, ...] = ()
    complete_objectives: tuple[int, ...] = ()
    set_flags: tuple[str, ...] = ()
    resolves_task: bool = False
    minutes: int = 0


class Leitung:
    """Umpired exercise-control cell.

    When an LLM is configured, it judges the player's staff product against the
    scenario and current task. The engine still validates any proposed effects
    before state changes are applied. Without a model, deterministic fallback
    adjudication keeps fixture tests and offline play usable.
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
        "approve",
        "sign",
        "normandy",
        "overlord",
        "landing",
        "beach",
        "airborne",
        "mulberry",
        "cherbourg",
        "neptune",
        "five-division",
        "joint concept",
    )
    _AREA_RECOMMENDATION = (
        "recommend",
        "suggest",
        "select",
        "choose",
        "propose",
        "favor",
        "prefer",
        "area",
        "coast",
    )
    _NORMANDY_FACTORS = (
        "surprise",
        "beach",
        "beaches",
        "fighter",
        "air",
        "cherbourg",
        "lodgment",
        "fifteenth",
        "defense",
        "defences",
        "defenses",
        "landing craft",
        "weather",
        "tide",
        "mulberry",
        "sustain",
    )
    _STAFF_ESTIMATE_COST = 15

    def __init__(self, dm: DungeonMaster):
        self.dm = dm
        self.engine = dm.engine

    def adjudicate(self, player_input: str, event: TimelineEvent) -> ControlResult:
        product = self.parse_staff_product(player_input)
        ruling = self._llm_judge(product, event)
        if ruling:
            proposal = self._proposal_from_ruling(product, ruling)
            findings = self._findings_from_ruling(product, event, proposal, ruling)
            self._record(product, findings)
            return ControlResult(proposal=proposal, product=product, findings=findings)

        llm_failed = self.dm.llm.enabled
        proposal = self.dm.interpret(player_input, event)
        assessment = self._assess_staff_product(product, event)
        if assessment and not proposal.effects:
            proposal = self._proposal_from_assessment(product, proposal, assessment)
        if self._is_generic_decisive_containment(product, event, proposal):
            proposal.effects = []
        findings = self._findings(product, event, proposal, assessment)
        if llm_failed:
            findings.insert(
                0,
                "LLM judge unavailable or returned invalid JSON; used offline adjudication.",
            )
        self._record(product, findings)
        return ControlResult(proposal=proposal, product=product, findings=findings)

    def _llm_judge(
        self, product: StaffProduct, event: TimelineEvent
    ) -> JudgeRuling | None:
        if not self.dm.llm.enabled:
            return None

        raw = self.dm.llm.generate(
            self._judge_prompt(product, event),
            system=self._judge_system_prompt(),
        )
        return self._parse_judge_ruling(raw, event)

    def _judge_system_prompt(self) -> str:
        return (
            "You are Leitung, the exercise-control judge for an umpired staff "
            "exercise. Judge the player's staff product against the scenario facts, "
            "current task, objectives, constraints, and known history/domain facts. "
            "Do not require exact menu wording. A bad but relevant recommendation "
            "should receive a concrete critique and may resolve the local planning "
            "paper; gibberish or unrelated text should not resolve the task. "
            "Return ONLY valid JSON with this schema: "
            '{"findings":["short umpire finding"],"knowledge":["facts recorded"],'
            '"completeObjectiveIds":[1],"setFlags":["flag-name"],'
            '"resolvesTask":true,"minutes":15}. '
            "Use only objective ids and flags listed in the prompt. Set a flag only "
            "when the product actually satisfies the decisive branch condition."
        )

    def _judge_prompt(self, product: StaffProduct, event: TimelineEvent) -> str:
        scenario = self.engine.bundle.scenario
        params = scenario.scenario_parameters
        tech = scenario.technical_environment
        mechanics = scenario.game_mechanics
        objectives = [
            {
                "id": o.id,
                "name": o.name,
                "description": o.description,
                "successCriteria": o.success_criteria,
            }
            for o in self.engine.bundle.objectives
            if o.id in event.objective_ids
        ]
        owned_flags = self.engine.flags_owned_by(event)

        context = {
            "scenario": {
                "name": scenario.name,
                "description": scenario.description,
                "objectives": params.objectives if params else "",
                "politicalContext": params.political_context if params else "",
                "rulesOfEngagement": params.rules_of_engagement if params else "",
                "victoryConditions": params.victory_conditions if params else "",
                "terrain": tech.network_topology if tech else "",
                "services": tech.services if tech else "",
                "assets": tech.assets if tech else "",
                "defenses": tech.defenses if tech else [],
                "vulnerabilities": [
                    {
                        "asset": v.asset,
                        "issue": v.cve,
                        "severity": v.severity,
                    }
                    for v in (tech.vulnerabilities if tech else [])
                ],
                "deadlineMinutes": (
                    mechanics.containment_deadline_minutes if mechanics else None
                ),
                "deadlineLabel": mechanics.deadline_label if mechanics else "",
                "decisiveActionLabel": (
                    mechanics.decisive_action_label if mechanics else ""
                ),
            },
            "currentTask": {
                "number": event.number,
                "time": event.time,
                "assigned": event.assigned,
                "description": event.description,
                "objectiveIdsAllowed": event.objective_ids,
                "branchFlagsAllowed": owned_flags,
            },
            "objectivesAllowed": objectives,
            "playerProduct": {
                "raw": product.raw_text,
                "priority": product.priority,
                "intent": product.intent,
                "plan": product.plan,
                "assumptions": product.assumptions,
                "informationRequests": product.information_requests,
                "riskAcceptance": product.risk_acceptance,
                "isStructured": product.is_structured,
            },
        }
        return json.dumps(context, ensure_ascii=True, indent=2)

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
        self,
        product: StaffProduct,
        event: TimelineEvent,
        proposal: Proposal,
        assessment: StaffAssessment | None = None,
    ) -> list[str]:
        findings: list[str] = []
        task_text = event.description.lower()
        order_text = self._order_text(product)

        if product.is_structured:
            findings.append("Leitung received a staff product, not just a move.")
        elif assessment:
            findings.append("Leitung received an unstructured staff recommendation.")
        else:
            findings.append("Leitung treated this as a hasty oral order.")

        if assessment:
            findings.append(assessment.finding)

        if product.assumptions:
            findings.append(
                "Assumptions logged for critique: " + "; ".join(product.assumptions[:3])
            )

        owns_branch = bool(self.engine.flags_owned_by(event))
        if owns_branch:
            deadline = self.engine.containment_deadline
            mechanics = self.engine.bundle.scenario.game_mechanics
            decision_label = (
                mechanics.deadline_label.lower()
                if mechanics
                else "containment fuse"
            )
            if decision_label.endswith(" fuse"):
                decision_label = decision_label[:-5]
            if deadline is not None:
                findings.append(
                    f"Time-distance check: this task must satisfy {decision_label} "
                    f"before the {deadline}m deadline."
                )
            if any(k in order_text for k in self._CONTAINMENT) and not self._has_specific_containment_detail(product):
                findings.append(
                    "Umpire ruling: generic containment order lacks concrete tasking; the decisive threat keeps running."
                )
            elif any(k in order_text for k in self._CONTAINMENT):
                findings.append(
                    f"Umpire ruling: order addresses the decisive {decision_label} constraint."
                )
            else:
                findings.append(
                    f"Umpire ruling: order does not satisfy {decision_label}; "
                    "the clock keeps running."
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

    def _findings_from_ruling(
        self,
        product: StaffProduct,
        event: TimelineEvent,
        proposal: Proposal,
        ruling: JudgeRuling,
    ) -> list[str]:
        findings: list[str] = []
        if product.is_structured:
            findings.append("Leitung received a staff product for LLM adjudication.")
        else:
            findings.append("Leitung received free text for LLM adjudication.")

        findings.extend(ruling.findings)

        if product.assumptions:
            findings.append(
                "Assumptions logged for critique: " + "; ".join(product.assumptions[:3])
            )

        if product.information_requests:
            findings.append(
                "Recon requests queued against the player picture: "
                + "; ".join(product.information_requests[:3])
            )

        if self.engine.flags_owned_by(event) and not ruling.set_flags:
            mechanics = self.engine.bundle.scenario.game_mechanics
            decision_label = (
                mechanics.deadline_label.lower() if mechanics else "decisive branch"
            )
            findings.append(
                f"Umpire ruling: product does not satisfy {decision_label}; "
                "the clock keeps running."
            )

        if not proposal.effects and not proposal.resolves_task:
            findings.append("No adjudicated effect: the product lacked an executable tasking.")
        return findings

    def _proposal_from_ruling(
        self, product: StaffProduct, ruling: JudgeRuling
    ) -> Proposal:
        effects: list[Effect] = []
        effects.extend(Effect(EffectKind.ADD_KNOWLEDGE, k) for k in ruling.knowledge)
        effects.extend(
            Effect(EffectKind.COMPLETE_OBJECTIVE, oid)
            for oid in ruling.complete_objectives
        )
        effects.extend(Effect(EffectKind.SET_FLAG, flag) for flag in ruling.set_flags)

        return Proposal(
            action_label=product.raw_text or "(nothing)",
            effects=effects,
            resolves_task=ruling.resolves_task,
            minutes=ruling.minutes,
        )

    def _parse_judge_ruling(
        self, raw: str | None, event: TimelineEvent
    ) -> JudgeRuling | None:
        data = self._parse_json_object(raw)
        if not data:
            return None

        findings = tuple(_clean_strings(data.get("findings"), limit=5))
        if not findings:
            return None

        allowed_objectives = set(event.objective_ids)
        allowed_flags = set(self.engine.flags_owned_by(event))
        complete_objectives = tuple(
            oid
            for oid in _clean_ints(data.get("completeObjectiveIds"))
            if oid in allowed_objectives
        )
        set_flags = tuple(
            flag for flag in _clean_strings(data.get("setFlags")) if flag in allowed_flags
        )

        resolves_task = bool(data.get("resolvesTask", False))
        minutes = self._clamp_minutes(data.get("minutes"))
        if resolves_task and minutes == 0:
            minutes = self._STAFF_ESTIMATE_COST

        knowledge = tuple(_clean_strings(data.get("knowledge"), limit=5))
        if not knowledge and (resolves_task or complete_objectives or set_flags):
            knowledge = findings[:1]

        return JudgeRuling(
            findings=findings,
            knowledge=knowledge,
            complete_objectives=complete_objectives,
            set_flags=set_flags,
            resolves_task=resolves_task,
            minutes=minutes,
        )

    @staticmethod
    def _parse_json_object(raw: str | None) -> dict | None:
        if not raw:
            return None
        text = raw.strip()
        start, end = text.find("{"), text.rfind("}")
        if start == -1 or end == -1 or end < start:
            return None
        try:
            data = json.loads(text[start : end + 1])
        except json.JSONDecodeError:
            return None
        return data if isinstance(data, dict) else None

    @staticmethod
    def _clamp_minutes(value) -> int:
        try:
            minutes = int(value)
        except (TypeError, ValueError):
            return 0
        return min(60, max(0, minutes))

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
        order_text = self._order_text(product)
        return any(k in order_text for k in self._SPECIFIC_CONTAINMENT)

    def _assess_staff_product(
        self, product: StaffProduct, event: TimelineEvent
    ) -> StaffAssessment | None:
        if not self._is_assault_area_estimate(event):
            return None

        order_text = self._order_text(product)
        if not any(k in order_text for k in self._AREA_RECOMMENDATION):
            return None

        if "brittany" in order_text:
            finding = (
                "Area estimate rejected: Brittany is outside the presented "
                "Pas-de-Calais-versus-Normandy trade space. The rationale cites "
                "personal preference instead of terrain, enemy, air-cover, or "
                "sustainment factors."
            )
            return StaffAssessment(finding=finding, knowledge=finding)

        if "pas-de-calais" in order_text or "calais" in order_text:
            finding = (
                "Area estimate challenged: Pas-de-Calais shortens the crossing, "
                "but the common picture says it faces the strongest defenses and "
                "the German Fifteenth Army."
            )
            return StaffAssessment(finding=finding, knowledge=finding)

        if "normandy" in order_text:
            has_factors = any(k in order_text for k in self._NORMANDY_FACTORS)
            if has_factors:
                finding = (
                    "Area estimate accepted: Normandy matches the common picture "
                    "for surprise, usable beaches, fighter cover, and expansion "
                    "toward Cherbourg."
                )
                return StaffAssessment(
                    finding=finding,
                    knowledge=finding,
                    complete_objectives=tuple(event.objective_ids),
                )
            finding = (
                "Area estimate provisionally supports Normandy, but the product "
                "still needs the decisive terrain, enemy, and air-cover factors."
            )
            return StaffAssessment(finding=finding, knowledge=finding)

        finding = (
            "Area estimate incomplete: the product recommends an assault area "
            "without tying it to the common picture's terrain, enemy, air-cover, "
            "landing-craft, or sustainment constraints."
        )
        return StaffAssessment(finding=finding, knowledge=finding)

    def _proposal_from_assessment(
        self,
        product: StaffProduct,
        proposal: Proposal,
        assessment: StaffAssessment,
    ) -> Proposal:
        effects = [Effect(EffectKind.ADD_KNOWLEDGE, assessment.knowledge)]
        effects.extend(
            Effect(EffectKind.COMPLETE_OBJECTIVE, oid)
            for oid in assessment.complete_objectives
        )
        return Proposal(
            action_label=product.raw_text or proposal.action_label,
            effects=effects,
            resolves_task=True,
            minutes=proposal.minutes or self._STAFF_ESTIMATE_COST,
        )

    @staticmethod
    def _is_assault_area_estimate(event: TimelineEvent) -> bool:
        text = event.description.lower()
        return "area estimate" in text and "assault area" in text

    @staticmethod
    def _order_text(product: StaffProduct) -> str:
        return " ".join(
            [
                product.priority,
                product.intent,
                product.plan,
                " ".join(product.assumptions),
                " ".join(product.information_requests),
                product.risk_acceptance,
            ]
        ).lower()

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


def _clean_strings(value, limit: int | None = None) -> list[str]:
    if isinstance(value, str):
        items = [value]
    elif isinstance(value, list):
        items = value
    else:
        return []

    out: list[str] = []
    for item in items:
        text = str(item).strip()
        if text:
            out.append(text)
        if limit is not None and len(out) >= limit:
            break
    return out


def _clean_ints(value) -> list[int]:
    if isinstance(value, int):
        items = [value]
    elif isinstance(value, list):
        items = value
    else:
        return []

    out: list[int] = []
    for item in items:
        try:
            out.append(int(item))
        except (TypeError, ValueError):
            continue
    return out
