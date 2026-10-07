# Phishing Drill: First Contact — exercise plan

*Rendered from `phishing-drill.scenario.json` (schema 1.1.0). A view, not a source: regenerate it after every edit. What gets signed is the plan; what gets loaded is the document.*

A small SOC-defense rehearsal. You are the on-shift Blue Team analyst at Meridian Logistics. An adversary opens with a spear-phishing campaign against the finance team. How fast you detect and contain it decides whether the intrusion stops at the inbox or turns into hands-on-keyboard lateral movement.

## Background (White Cell)

Routine business operations. No heightened alert posture. Leadership expects the SOC to handle day-to-day threats without escalation unless containment fails.

## The audience

**What they may and may not do.** Blue Team may investigate, quarantine, block, and notify users. No offensive action. Escalate to IR lead only if hosts are confirmed compromised.

| Side | Alignment |
|---|---|
| Meridian Logistics (Home) | friendly |
| Crimson Tide Group | adversary |

## How it is played

| Rule of play |  |
|---|---|
| Runs for | 1h |
| Pacing | turn-based |
| Adjudication | automated |
| Telemetry | logs, network, endpoint |

### How far it can go

Inbox -> Endpoint -> Identity -> Lateral

### If play diverges

Player containment at step 4 sets flag 'contained'. Red Team step 5 forks: contained -> blocked-retry path; not contained -> credential-theft path.

## The adversary

### Crimson Tide — criminal, capability 3/5

**Techniques.** T1021.001, T1078, T1566.001 (ATT&CK ids; the validator resolves each one).

## The terrain

**Topology.** Flat /24 corporate LAN, single VLAN for finance, perimeter firewall with default-allow egress.

**Assets.** FIN-WS-04 (finance workstation), FS01 (file server), DC01 (domain controller)

**Services.** Microsoft 365 mail, on-prem file server (FS01), domain controller (DC01).

| Defense | Covers | What it does |
|---|---|---|
| Email gateway with URL rewriting | — | — |
| EDR on workstations | — | — |
| SIEM with 15-min alert SLA | — | — |

| Weakness | On | Severity | What it is |
|---|---|---|---|
| CVE-2023-23397 | FIN-WS-04 | high | — |

### Who GHOSTS simulates

13 simulated people. Everyone else in the exercise is a person playing.

| Pool | Count | Who they are |
|---|---|---|
| Finance Staff | 12 | — |
| SOC Analyst | 1 | — |

## Entities

Everyone and everything the document names as part of the graph — people, systems, organizations, and the rest of the recommended vocabulary — so nothing here is absent from the plan.

| Id | Name | Type | Description | Provenance |
|---|---|---|---|---|
| crimson-tide | Crimson Tide | ThreatActor | Financially motivated criminal crew. Opens with credential phishing, pivots on valid accounts. | operator, confidence 1, reviewed |
| dana-okafor | Dana Okafor | Person | Finance clerk at Meridian. First to open the phishing email; works on FIN-WS-04. | operator, confidence 1, reviewed |
| fin-ws-04 | FIN-WS-04 | System | Finance workstation. Patient zero if credentials are harvested. | operator, confidence 1, reviewed |
| fs01 | FS01 | System | On-prem file server. Lateral-movement target over SMB. | operator, confidence 1, reviewed |

## Master Scenario Events List

In exercise-planning terms, each row below is an inject: a controller-triggered stimulus with a time or a trigger, an owner, and the response it is meant to provoke.

9 beats. Every one names who owns it, what the audience is expected to do, and what they would have to see in order to do it.

| # | When | Owner | Beat | Expected response | Indicators |
|---|---|---|---|---|---|
| 1 | T+10m | white-cell | Suspicious email alert reaches the SOC queue | — | — |
| 2 | T+0m | white-cell | Exercise start. It is a quiet Tuesday morning shift. The SIEM dashboard is green. You hold the on-shift SOC analyst seat at Meridian Logistics, responsible for the finance VLAN. | — | — |
| 3 | T+5m | red-team | Crimson Tide sends a spear-phishing email to twelve finance staff. It impersonates the company's payroll provider and carries a link to a credential-harvesting page styled like the Microsoft 365 login. | — | — |
| 4 | T+10m | white-cell | The email gateway fires a low-confidence alert into your SOC queue: 'Possible credential phishing - external sender, lookalike domain payroll-meridian[.]com, 12 recipients.' Two recipients have already opened the message. | — | — |
| 5 | T+12m | blue-team | Your move. The alert is in your queue and the clock is running. What do you do? You can investigate the message and the lookalike domain, quarantine the email from all inboxes, block the malicious domain at the gateway, notify finance staff, or wait and watch. | — | — |
| 6 | T+15m, when `flag:contained` | red-team | Containment held. By the time Crimson Tide's harvesting page would have collected a password, the email is gone from every inbox and the domain is blocked. The adversary probes for another way in and finds the door shut. The campaign stalls at the inbox. | — | — |
| 7 | T+15m, when `!contained` | red-team | No containment came. A finance clerk on FIN-WS-04 enters their M365 password into the harvesting page. Crimson Tide now holds valid credentials (T1078) and authenticates to the file server FS01 over SMB, moving laterally off the first host. The intrusion is now hands-on-keyboard. | — | — |
| 8 | T+20m | blue-team | Situation update. Either you stopped this at the inbox or you are now chasing an adversary on the network. Assess what you know and decide your next action: confirm scope, isolate the affected host, reset exposed credentials, or stand the team down. | — | — |
| 9 | T+30m | white-cell | Exercise complete. The white cell calls end-of-scenario and begins the after-action review. | — | — |

### Beat by beat

**1. Suspicious email alert reaches the SOC queue — T+10m**, white-cell. Fires on the clock.

**2. event-1 — T+0m**, white-cell. Exercise start. It is a quiet Tuesday morning shift. The SIEM dashboard is green. You hold the on-shift SOC analyst seat at Meridian Logistics, responsible for the finance VLAN. Fires on the clock.

**3. event-2 — T+5m**, red-team. Crimson Tide sends a spear-phishing email to twelve finance staff. It impersonates the company's payroll provider and carries a link to a credential-harvesting page styled like the Microsoft 365 login. Fires on the clock.

**4. event-3 — T+10m**, white-cell. The email gateway fires a low-confidence alert into your SOC queue: 'Possible credential phishing - external sender, lookalike domain payroll-meridian[.]com, 12 recipients.' Two recipients have already opened the message. Fires on the clock; graded against objective 1.

**5. event-4 — T+12m**, blue-team. Your move. The alert is in your queue and the clock is running. What do you do? You can investigate the message and the lookalike domain, quarantine the email from all inboxes, block the malicious domain at the gateway, notify finance staff, or wait and watch. Fires on the clock; graded against objective 1, 2.

**6. event-5 — T+15m, when `flag:contained`**, red-team. Containment held. By the time Crimson Tide's harvesting page would have collected a password, the email is gone from every inbox and the domain is blocked. The adversary probes for another way in and finds the door shut. The campaign stalls at the inbox. Fires when `flag:contained` holds; graded against objective 2.

**7. event-6 — T+15m, when `!contained`**, red-team. No containment came. A finance clerk on FIN-WS-04 enters their M365 password into the harvesting page. Crimson Tide now holds valid credentials (T1078) and authenticates to the file server FS01 over SMB, moving laterally off the first host. The intrusion is now hands-on-keyboard. Fires when `!contained` holds.

**8. event-7 — T+20m**, blue-team. Situation update. Either you stopped this at the inbox or you are now chasing an adversary on the network. Assess what you know and decide your next action: confirm scope, isolate the affected host, reset exposed credentials, or stand the team down. Fires on the clock; graded against objective 2.

**9. event-8 — T+30m**, white-cell. Exercise complete. The white cell calls end-of-scenario and begins the after-action review. Fires on the clock.

## What counts as success

**Why this exercise exists.** Detect and contain a spear-phishing intrusion before it becomes lateral movement.

**Victory conditions.** WIN: the phishing campaign is contained before the adversary obtains valid credentials (objective 2 met). LOSS: the adversary moves laterally to a second host.

**How it is measured.** time-to-detect, time-to-contain, lateral-movement-prevented

| Id | Objective | Type | Priority | Met when | Assigned |
|---|---|---|---|---|---|
| 1 | Detect the phishing campaign | — | — | Player investigates the alert / lookalike domain before acting blindly. | blue-team |
| 2 | Contain before lateral movement | — | — | The 'contained' flag is set at step 4, steering the timeline onto the blocked-retry branch. | blue-team |

