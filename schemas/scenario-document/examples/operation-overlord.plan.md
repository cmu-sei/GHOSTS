# OPERATION OVERLORD: The Normandy Decision — exercise plan

*Rendered from `operation-overlord.scenario.json` (schema 1.1.0). A view, not a source: regenerate it after every edit. What gets signed is the plan; what gets loaded is the document.*

January 1944. You sit with the Supreme Headquarters Allied Expeditionary Force joint planning staff. The COSSAC outline must become an executable plan for opening a lodgment in Normandy. Landing frontage, air cover, naval lift, deception, and sustainment all compete for scarce resources. Eisenhower expects a coherent recommendation before the conference closes.

## Background (White Cell)

The Combined Chiefs have directed a cross-Channel invasion in 1944. Allied unity must be preserved while American, British, Canadian, air, naval, and ground requirements are reconciled.

## The audience

**Who.** Blue Team (SHAEF Joint Planner).

**What they may and may not do.** Plan within allocated landing craft and tactical air range. Preserve operational surprise, identify assumptions, and state which risks the Supreme Commander must accept.

| Side | Alignment |
|---|---|
| Allied Expeditionary Force | friendly |
| German-occupied France | adversary |

## How it is played

| Rule of play |  |
|---|---|
| Runs for | 1h |
| Pacing | turn-based |
| Adjudication | automated |
| Deadline | T+30m — COMMAND DECISION |
| Telemetry | logs, chat |

### How far it can go

Strategic directive -> operational concept -> joint force allocation -> NEPTUNE assault plan

### If play diverges

Approving the decision brief at step 5 sets flag 'overlord-plan-approved'. Step 6 presents command approval when set; step 7 presents a planning failure when unset at the command deadline.

## The adversary

### German Seventh and Fifteenth Armies — peer military, capability 5/5

**Capabilities in plain words.** Atlantic Wall coastal defenses, mobile armored reserve, coastal artillery, rapid reinforcement

## The terrain

**Topology.** English Channel crossing from southern England to a broad Normandy lodgment between the Cotentin Peninsula and the Orne River.

**Assets.** Five assault divisions, three airborne divisions, landing ships and craft, Mulberry artificial harbors, PLUTO pipeline, tactical air forces.

**Services.** Naval gunfire support, tactical air cover, airborne lift, landing craft, engineer support, deception, and cross-Channel logistics.

| Defense | Covers | What it does |
|---|---|---|
| ULTRA-derived intelligence picture | — | — |
| Allied air superiority | — | — |
| Naval fire support | — | — |
| Operation FORTITUDE deception plan | — | — |

| Weakness | On | Severity | What it is |
|---|---|---|---|
| No major port available on D-Day | Normandy lodgment | high | — |
| Weather, tide, and landing-craft constraints | Assault force | high | — |

### Who GHOSTS simulates

9 simulated people. Everyone else in the exercise is a person playing.

| Pool | Count | Who they are |
|---|---|---|
| SHAEF Joint Planning Staff | 1 | — |
| Allied Assault Divisions | 5 | — |
| Allied Airborne Divisions | 3 | — |

## Entities

Everyone and everything the document names as part of the graph — people, systems, organizations, and the rest of the recommended vocabulary — so nothing here is absent from the plan.

| Id | Name | Type | Description | Provenance |
|---|---|---|---|---|
| shaef | SHAEF | Organization | Supreme Headquarters Allied Expeditionary Force. | operator, confidence 1, not reviewed |
| german-seventh-and-fifteenth-armies | German Seventh and Fifteenth Armies | ThreatActor | German field armies defending France and the Channel coast. | operator, confidence 1, not reviewed |
| normandy-lodgment | Normandy Lodgment | Location | The intended Allied lodgment from the Cotentin Peninsula to the Orne. | operator, confidence 1, not reviewed |
| cross-channel-lift | Cross-Channel Lift | Resource | Landing ships, landing craft, and airborne lift allocated to NEPTUNE. | operator, confidence 1, not reviewed |

## Master Scenario Events List

In exercise-planning terms, each row below is an inject: a controller-triggered stimulus with a time or a trigger, an owner, and the response it is meant to provoke.

10 beats. Every one names who owns it, what the audience is expected to do, and what they would have to see in order to do it.

| # | When | Owner | Beat | Expected response | Indicators |
|---|---|---|---|---|---|
| 1 | T+0m | white-cell | Supreme Commander calls the OVERLORD planning conference to order | — | — |
| 2 | 21 JAN 44 / 0800 | white-cell | The Supreme Commander opens the planning conference at St. Paul's School in London. The Combined Chiefs' directive is plain: enter the continent, defeat Germany, and build the force required for operations into the heart of Europe. Your staff must turn the inherited COSSAC outline into a command decision. | — | — |
| 3 | 21 JAN 44 / 0810 | green-cell | The joint intelligence and logistics teams post the common picture. Pas-de-Calais offers the shortest crossing but faces the strongest defenses. Normandy offers surprise and beaches within fighter range, but no major port is available at the lodgment. Landing craft, weather, tides, and the rate of German reinforcement define the problem. | — | — |
| 4 | 21 JAN 44 / 0820 | blue-team | PLANNING PAPER 1 — Area estimate. The staff must recommend the assault area. Pas-de-Calais shortens the crossing but concentrates against the German Fifteenth Army; Normandy offers operational surprise, usable beaches, and room to expand toward Cherbourg. State the decisive terrain, enemy, and air-cover factors. | — | — |
| 5 | 21 JAN 44 / 0820 | blue-team | PLANNING PAPER 2 — Joint force and sustainment estimate. A broad front improves the chance of a viable lodgment but consumes scarce landing craft. The staff must integrate five assault divisions, airborne flank protection, naval gunfire, tactical air, Mulberry harbors, and early capture of Cherbourg. | — | — |
| 6 | 21 JAN 44 / 0820 | blue-team | PLANNING PAPER 3 — Supreme Commander's decision brief. Recommend a five-division assault across the Utah, Omaha, Gold, Juno, and Sword sectors; airborne forces protect both flanks; naval and air plans isolate the battlefield; FORTITUDE holds German attention toward Pas-de-Calais; Mulberries sustain the lodgment while Cherbourg is opened. Approve, revise, or defer the joint concept. | — | — |
| 7 | 21 JAN 44 / 0850, when `flag:overlord-plan-approved` | white-cell | The Supreme Commander approves the expanded Normandy concept. Five assault divisions will land on a broad front, airborne forces will secure the flanks, and the joint staff will develop the assault as Operation NEPTUNE. Landing-craft allocations are reopened to support the decision. | — | — |
| 8 | 21 JAN 44 / 0850, when `!overlord-plan-approved` | white-cell | The conference adjourns without a unified recommendation. Air, naval, and ground staffs continue against different assumptions; landing craft remain allocated to competing demands. The cross-Channel timetable slips while the operational concept returns for revision. | — | — |
| 9 | 21 JAN 44 / 0900 | blue-team | The conference secretary asks for your final staff action. Publish the warning order and assign the NEPTUNE annexes if the concept was approved, or record the unresolved assumptions and direct a rapid revision if it was not. | — | — |
| 10 | 21 JAN 44 / 0910 | white-cell | Exercise control closes the planning session and begins the staff critique. The record now shows whether the team identified the decisive constraints, protected the command decision, and produced an executable operational concept. | — | — |

### Beat by beat

**1. Supreme Commander calls the OVERLORD planning conference to order — T+0m**, white-cell. Fires on the clock.

**2. event-1 — 21 JAN 44 / 0800**, white-cell. The Supreme Commander opens the planning conference at St. Paul's School in London. The Combined Chiefs' directive is plain: enter the continent, defeat Germany, and build the force required for operations into the heart of Europe. Your staff must turn the inherited COSSAC outline into a command decision. Fires on the clock.

**3. event-2 — 21 JAN 44 / 0810**, green-cell. The joint intelligence and logistics teams post the common picture. Pas-de-Calais offers the shortest crossing but faces the strongest defenses. Normandy offers surprise and beaches within fighter range, but no major port is available at the lodgment. Landing craft, weather, tides, and the rate of German reinforcement define the problem. Fires on the clock; graded against objective 1.

**4. event-3 — 21 JAN 44 / 0820**, blue-team. PLANNING PAPER 1 — Area estimate. The staff must recommend the assault area. Pas-de-Calais shortens the crossing but concentrates against the German Fifteenth Army; Normandy offers operational surprise, usable beaches, and room to expand toward Cherbourg. State the decisive terrain, enemy, and air-cover factors. Fires on the clock; graded against objective 1.

**5. event-4 — 21 JAN 44 / 0820**, blue-team. PLANNING PAPER 2 — Joint force and sustainment estimate. A broad front improves the chance of a viable lodgment but consumes scarce landing craft. The staff must integrate five assault divisions, airborne flank protection, naval gunfire, tactical air, Mulberry harbors, and early capture of Cherbourg. Fires on the clock; graded against objective 1.

**6. event-5 — 21 JAN 44 / 0820**, blue-team. PLANNING PAPER 3 — Supreme Commander's decision brief. Recommend a five-division assault across the Utah, Omaha, Gold, Juno, and Sword sectors; airborne forces protect both flanks; naval and air plans isolate the battlefield; FORTITUDE holds German attention toward Pas-de-Calais; Mulberries sustain the lodgment while Cherbourg is opened. Approve, revise, or defer the joint concept. Fires on the clock; graded against objective 2.

**7. event-6 — 21 JAN 44 / 0850, when `flag:overlord-plan-approved`**, white-cell. The Supreme Commander approves the expanded Normandy concept. Five assault divisions will land on a broad front, airborne forces will secure the flanks, and the joint staff will develop the assault as Operation NEPTUNE. Landing-craft allocations are reopened to support the decision. Fires when `flag:overlord-plan-approved` holds; graded against objective 2.

**8. event-7 — 21 JAN 44 / 0850, when `!overlord-plan-approved`**, white-cell. The conference adjourns without a unified recommendation. Air, naval, and ground staffs continue against different assumptions; landing craft remain allocated to competing demands. The cross-Channel timetable slips while the operational concept returns for revision. Fires when `!overlord-plan-approved` holds.

**9. event-8 — 21 JAN 44 / 0900**, blue-team. The conference secretary asks for your final staff action. Publish the warning order and assign the NEPTUNE annexes if the concept was approved, or record the unresolved assumptions and direct a rapid revision if it was not. Fires on the clock.

**10. event-9 — 21 JAN 44 / 0910**, white-cell. Exercise control closes the planning session and begins the staff critique. The record now shows whether the team identified the decisive constraints, protected the command decision, and produced an executable operational concept. Fires on the clock.

## What counts as success

**Why this exercise exists.** Produce a coherent joint operational concept for OVERLORD that can secure and sustain a lodgment in Normandy.

**Victory conditions.** WIN: the staff approves a specific five-division Normandy concept with airborne flank protection and a credible sustainment scheme. LOSS: the conference closes without an executable joint concept and the invasion timetable slips.

**How it is measured.** constraint identification, prioritization, joint integration, decision quality

| Id | Objective | Type | Priority | Met when | Assigned |
|---|---|---|---|---|---|
| 1 | Identify the decisive operational constraints | — | 2 | The player investigates the area and joint-force estimates before committing the command. | blue-team |
| 2 | Prevent the OVERLORD concept from stalling | — | — | The 'overlord-plan-approved' flag is set on the Supreme Commander's decision brief, steering the conference onto the approved branch. | blue-team |

