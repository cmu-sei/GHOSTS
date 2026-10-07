# SOC Morning: Clear the Queue by Lunch — exercise plan

*Rendered from `soc-morning.scenario.json` (schema 1.1.0). A view, not a source: regenerate it after every edit. What gets signed is the plan; what gets loaded is the document.*

You are the on-shift Blue Team analyst at Meridian Logistics. It's Monday and three tickets are already stacked in your queue: a phishing report, an EDR ransomware alert, and a VPN account lockout from a traveling exec. They're all open at once — you decide the order. The ransomware alert is the one that bites if you dawdle. Clear the queue before it turns into a bad day, and try to make lunch.

## Background (White Cell)

Routine Monday. Leadership expects the SOC to clear day-to-day tickets without escalation unless something is actively spreading.

## The audience

**What they may and may not do.** Blue Team may investigate, quarantine, block, reset accounts, and notify users. Escalate to IR lead only if a host is confirmed encrypting.

| Side | Alignment |
|---|---|
| Meridian Logistics (Home) | friendly |
| Cinder Wolf Group | adversary |

## How it is played

| Rule of play |  |
|---|---|
| Runs for | 45m |
| Pacing | turn-based |
| Adjudication | automated |
| Deadline | T+30m |
| Telemetry | logs, network, endpoint |

### How far it can go

Inbox -> Endpoint -> Identity -> Lateral

### If play diverges

Containing the ransomware ticket (step 4) sets flag 'ransomware-contained'. If it isn't set by the 30m containment fuse, the threat detonates immediately and locks the loss branch. Red Team step 6 fires if contained (threat stalls); step 7 fires if !contained (encryption spreads).

## The adversary

### Cinder Wolf — ransomware, capability 4/5

**Techniques.** T1021.001, T1078, T1486, T1566.001 (ATT&CK ids; the validator resolves each one).

## The terrain

**Topology.** Flat /24 corporate LAN, finance VLAN, perimeter firewall with default-allow egress, split-tunnel VPN.

**Assets.** FIN-WS-04 (finance workstation), FS01 (file server), DC01 (domain controller), VPN-GW

**Services.** Microsoft 365 mail, on-prem file server (FS01), domain controller (DC01), VPN concentrator.

| Defense | Covers | What it does |
|---|---|---|
| Email gateway with URL rewriting | — | — |
| EDR on workstations | — | — |
| SIEM with 15-min alert SLA | — | — |
| MFA on VPN | — | — |

| Weakness | On | Severity | What it is |
|---|---|---|---|
| CVE-2023-23397 | FIN-WS-04 | high | — |

### Who GHOSTS simulates

14 simulated people. Everyone else in the exercise is a person playing.

| Pool | Count | Who they are |
|---|---|---|
| Finance Staff | 12 | — |
| Traveling Executive | 1 | — |
| SOC Analyst | 1 | — |

## Entities

Everyone and everything the document names as part of the graph — people, systems, organizations, and the rest of the recommended vocabulary — so nothing here is absent from the plan.

| Id | Name | Type | Description | Provenance |
|---|---|---|---|---|
| cinder-wolf | Cinder Wolf | ThreatActor | Ransomware crew. Opens with phishing, deletes shadow copies, then encrypts file shares. | operator, confidence 1, reviewed |
| dana-okafor | Dana Okafor | Person | Finance clerk at Meridian. Reported the phishing email; works on FIN-WS-04. | operator, confidence 1, reviewed |
| fin-ws-04 | FIN-WS-04 | System | Finance workstation. Patient zero for the ransomware precursor. | operator, confidence 1, reviewed |
| fs01 | FS01 | System | On-prem file server. Encryption target over SMB. | operator, confidence 1, reviewed |

## Master Scenario Events List

In exercise-planning terms, each row below is an inject: a controller-triggered stimulus with a time or a trigger, an owner, and the response it is meant to provoke.

10 beats. Every one names who owns it, what the audience is expected to do, and what they would have to see in order to do it.

| # | When | Owner | Beat | Expected response | Indicators |
|---|---|---|---|---|---|
| 1 | T+0m | white-cell | Three tickets already open in the SOC queue | — | — |
| 2 | T+0m | white-cell | Exercise start. Monday, 08:30. You take the on-shift SOC analyst seat at Meridian Logistics. The queue is not empty — three tickets are already waiting, and the clock to lunch is running. | — | — |
| 3 | T+2m | white-cell | Your queue populates. Ticket #1: a finance clerk reports a suspicious payroll email. Ticket #2: EDR flagged a possible ransomware precursor on FIN-WS-04 (a service creating shadow-copy deletions). Ticket #3: a traveling exec is locked out of the VPN. All three are yours. | — | — |
| 4 | T+5m | blue-team | TICKET #1 — Phishing report. A finance clerk forwarded an email impersonating the payroll provider, carrying a link to a lookalike Microsoft 365 login. Twelve staff received it; two have opened it. What do you do about this one? | — | — |
| 5 | T+5m | blue-team | TICKET #2 — Ransomware precursor. EDR shows a process on FIN-WS-04 deleting volume shadow copies (T1486 precursor). This is the one that spreads if you leave it. What do you do about this one? | — | — |
| 6 | T+5m | blue-team | TICKET #3 — VPN lockout. A traveling executive is locked out of the VPN after repeated MFA prompts. Could be a fat-fingered password on the road, could be someone spraying their account. What do you do about this one? | — | — |
| 7 | T+40m, when `flag:ransomware-contained` | red-team | The shadow-copy deletion was killed and FIN-WS-04 isolated before encryption began. Cinder Wolf's payload never detonates on the share. The precursor stalls out on a single quarantined host. | — | — |
| 8 | T+40m, when `!ransomware-contained` | red-team | The precursor ran unchecked. Cinder Wolf's payload detonates on FIN-WS-04, reaches FS01 over SMB, and begins encrypting the finance file share (T1486). The morning just became an incident. | — | — |
| 9 | T+45m | blue-team | Situation update, late morning. Either the queue is clear and you're eyeing lunch, or you're chasing an encrypting host. Decide your last action of the morning: confirm scope, isolate the affected host, or stand the team down. | — | — |
| 10 | T+60m | white-cell | The white cell calls end-of-morning and begins the after-action review. Lunch is on the table — the question is whether you got to it. | — | — |

### Beat by beat

**1. Three tickets already open in the SOC queue — T+0m**, white-cell. Fires on the clock.

**2. event-1 — T+0m**, white-cell. Exercise start. Monday, 08:30. You take the on-shift SOC analyst seat at Meridian Logistics. The queue is not empty — three tickets are already waiting, and the clock to lunch is running. Fires on the clock.

**3. event-2 — T+2m**, white-cell. Your queue populates. Ticket #1: a finance clerk reports a suspicious payroll email. Ticket #2: EDR flagged a possible ransomware precursor on FIN-WS-04 (a service creating shadow-copy deletions). Ticket #3: a traveling exec is locked out of the VPN. All three are yours. Fires on the clock.

**4. event-3 — T+5m**, blue-team. TICKET #1 — Phishing report. A finance clerk forwarded an email impersonating the payroll provider, carrying a link to a lookalike Microsoft 365 login. Twelve staff received it; two have opened it. What do you do about this one?. Fires on the clock; graded against objective 1.

**5. event-4 — T+5m**, blue-team. TICKET #2 — Ransomware precursor. EDR shows a process on FIN-WS-04 deleting volume shadow copies (T1486 precursor). This is the one that spreads if you leave it. What do you do about this one?. Fires on the clock; graded against objective 1, 2.

**6. event-5 — T+5m**, blue-team. TICKET #3 — VPN lockout. A traveling executive is locked out of the VPN after repeated MFA prompts. Could be a fat-fingered password on the road, could be someone spraying their account. What do you do about this one?. Fires on the clock.

**7. event-6 — T+40m, when `flag:ransomware-contained`**, red-team. The shadow-copy deletion was killed and FIN-WS-04 isolated before encryption began. Cinder Wolf's payload never detonates on the share. The precursor stalls out on a single quarantined host. Fires when `flag:ransomware-contained` holds; graded against objective 2.

**8. event-7 — T+40m, when `!ransomware-contained`**, red-team. The precursor ran unchecked. Cinder Wolf's payload detonates on FIN-WS-04, reaches FS01 over SMB, and begins encrypting the finance file share (T1486). The morning just became an incident. Fires when `!ransomware-contained` holds.

**9. event-8 — T+45m**, blue-team. Situation update, late morning. Either the queue is clear and you're eyeing lunch, or you're chasing an encrypting host. Decide your last action of the morning: confirm scope, isolate the affected host, or stand the team down. Fires on the clock; graded against objective 2.

**10. event-9 — T+60m**, white-cell. The white cell calls end-of-morning and begins the after-action review. Lunch is on the table — the question is whether you got to it. Fires on the clock.

## What counts as success

**Why this exercise exists.** Clear the morning queue: triage every ticket and contain the ransomware alert before it spreads.

**Victory conditions.** WIN: the ransomware alert is contained before it encrypts a second host (objective 2 met). LOSS: encryption spreads across the file share.

**How it is measured.** time-to-triage, time-to-contain, tickets-cleared, made-lunch

| Id | Objective | Type | Priority | Met when | Assigned |
|---|---|---|---|---|---|
| 1 | Triage the morning queue | — | 2 | Player investigates the tickets before acting blindly. | blue-team |
| 2 | Contain the ransomware before it spreads | — | — | The 'ransomware-contained' flag is set on ticket #2, steering the timeline onto the stalled branch. | blue-team |

