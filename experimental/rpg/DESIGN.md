# GHOSTS RPG — Scenario Player (Zork Mode)

**v0.2 — 2026-06-30.** A single-player, Zork-style web game for cyber/cogwar mission
rehearsal, built on GHOSTS scenarios. The computer is the **Dungeon Master**: it
narrates what is happening, plays everyone except the player (including the OPFOR),
and offers the player choices. The player holds a **defensive role** and reacts; their
responses bend the game forward — immediately or at a future step.

Stack: **FastAPI** (Python, `ghosts_rpg`) backend + **modern Angular** frontend.
Ports: API `8095`, UI `4300` (mirrors prior RPG convention).

---

## 1. Locked decisions (2026-06-30)

| # | Decision | Choice |
|---|----------|--------|
| D1 | Scenario data + turn engine | **Self-contained engine.** The RPG owns its own turn/state/branching/scoring logic in Python. It pulls scenario data once at game start (live GHOSTS API *or* a JSON export) and then runs independently — **no live Execution coupling**. |
| D2 | DM brain | **LLM (Ollama) + deterministic fallback.** Narration and free-text parsing go through Ollama `/api/generate`; when no model is reachable, a deterministic templated narrator/parser keeps the game fully playable offline. |
| D3 | Player response model | **Free-text + offered options.** Each Blue-Team turn the DM presents a list of options *and* accepts typed free text; the DM parses free text into one of the turn's valid effects. |
| D4 | Player turn shape (2026-07-01) | **Worklist + lunch clock.** A run of adjacent Blue-Team events opens as *parallel tasks* the player clears in any order, not one linear step. Each action burns minutes off the exercise window (`GameMechanics.durationHours`); clearing the queue before it runs out earns the urgency bonus in the AAR. Action menus are **LLM-proposed, engine-validated** (deterministic scenario-derived fallback offline). A task's decisive action only sets the forward flags that task *owns* (linked via its objectives), so resolving one ticket can't satisfy another's branch. |
| D5 | Real stakes (2026-07-01) | **Containment fuse + hard loss.** A scenario may set `GameMechanics.containmentDeadlineMinutes`: if the steering flag(s) aren't set by the time the lunch clock passes that fuse, the threat **detonates mid-worklist** — the engine abandons every still-open ticket, jumps onto the loss branch, and the late containment can no longer be set (the ticket is gone). Scoring caps a detonated game in the F range and forfeits the made-lunch bonus. The lunch clock is now a hard ceiling too: hitting 0m ends the morning. Investigating the fuse-owning ticket reveals its urgency, so triage-before-action is the winning read. Numbers are tuned so blind clicking on noise tickets first blows the fuse (soc-morning: 45m budget, 30m fuse). |
| D6 | Sequential ticket reveal (2026-07-01) | **Tickets arrive one at a time; "table" to stack.** A worklist of adjacent Blue-Team events is *open* all at once (D4 — authoritative for the fuse, branch advance, and flag ownership), but only the **first** is *surfaced* to the player by default (Zork-style — one thing at a time). The player **tables** the current ticket (`next` / `table it`) to pull the next queued one onto the board without resolving it, stacking up to the full worklist if they choose; resolving a ticket auto-flows to the next. So three-tickets-open-at-once is reachable but **not the default**. Engine layer: `GameState.revealed_steps`, `Engine.revealed_worklist()`/`reveal_next()`/`has_unrevealed_tasks()`; the DM presents `revealed_worklist()`. HUD reports `shownTasks`/`queuedTasks`; the UI shows a dashed "table it · next ticket (N waiting)" chip. The opening White/Red/Green scene-setting beats still narrate up front as the facilitator lead-in. |
| D7 | Free-Kriegsspiel adjudication (2026-07-20) | **Blue submits staff products; Leitung adjudicates.** The look and terminal feel stay intact, but Blue input may now be a structured estimate/order (`Priority`, `Plan`, `Assumptions`, `Information Requests`, `Risk`) rather than only a command. `control.py` parses the staff product, records assumptions, checks deterministic constraints such as containment fuse and branch ownership, emits umpire findings, and passes validated effects to the engine. The LLM still cannot decide outcomes. The AAR includes Schlussbesprechung-style findings. |

Carried over from v0.1 (still true):
- **Engine authoritative; LLM is DM = narrate + parse + adjudicate only.** The LLM may
  only *propose* effects; the engine validates every proposal against the loaded
  scenario before applying it. **Branching is engine-evaluated, never LLM-chosen.**
- **World shape = steps, not rooms.** The player walks an authored scenario's ordered
  `ScenarioTimelineEvent`s. No map / locations / movement.
- DB: GHOSTS uses `EnsureCreated`, no EF migrations. The RPG adds **no** schema to
  GHOSTS; it persists its own session state locally (SQLite/JSON).

---

## 2. What a GHOSTS scenario gives the DM

Source of truth = a real GHOSTS **Scenario**
(`src/Ghosts.Api/Infrastructure/Models/Scenarios.cs`). Loaded once via three GETs:

1. `GET /api/scenarios/{id}` → framing + terrain + **timeline**
2. `GET /api/scenarios/{id}/builder/graph` → the cast (`ScenarioGraphDto`: Nodes/Edges)
3. `GET /api/objectives?scenarioId={id}` → scoring objectives

Fields the DM uses:

- **Framing** — `ScenarioParameters`: `Objectives`, `PoliticalContext`,
  `RulesOfEngagement`, `VictoryConditions`; `Nations` (alignment friendly/adversary/
  neutral), `ThreatActors` (type, capability, MITRE `Ttps`), `Injects` (trigger+title),
  `UserPools` (role+count).
- **Terrain** — `TechnicalEnvironment`: `NetworkTopology`, `Services`, `Assets`,
  `Defenses`, `Vulnerabilities` (asset/CVE/severity).
- **The flip-flop timeline** — `ScenarioTimeline` → `ScenarioTimelineEvent`s, each with
  `Number` (order), `Time`, `Description`, **`Assigned`**, `Status`, `ObjectiveIds`,
  and `TriggerKind` / `TriggerCondition` / `Schedule` for branching.
- **Cast** — entity/edge graph: `Person`, `Organization`, `System`, `ThreatActor`,
  `Vulnerability`, … with typed edges (`Targets`, `Exploits`, `ReportsTo`, …).
- **Scoring** — `Objective`s: `SuccessCriteria`, `Status`, `Score` (T/P/U), `Priority`.

### The `Assigned` field IS the flip-flop

`ScenarioTimelineEvent.Assigned` ∈ { `White Cell`, `Red Team`, `Blue Team`, `Green Cell` }.

- **Blue Team** = the **player's turn** (their defensive role). DM stops, narrates the
  situation, offers options, waits for input.
- **Red Team** = OPFOR — DM plays it.
- **White Cell** = injects / exercise control — DM plays it.
- **Green Cell** = friendly/support — DM plays it.

The DM narrates each run of non-Blue events ("something happens"), then hands control to
the player at the next Blue-Team event ("here is your situation; what do you do?").

---

## 3. Turn loop (engine-authoritative)

```
                ┌─────────────────────────────────────────────┐
                │ Engine: current step = lowest-Number         │
                │ non-terminal timeline event                  │
                └─────────────────────────────────────────────┘
                                   │
         ┌─────────────────────────┴───────────────────────────┐
         │ While next step.Assigned != Blue Team:               │
         │   DM narrates the Red/White/Green event (computer).  │
         │   Engine marks it complete, picks the forward branch │
         │   (lowest-Number later step whose TriggerCondition   │
         │    holds; linear order if no condition).             │
         └─────────────────────────┬───────────────────────────┘
                                   │  (reached a Blue Team step)
                                   ▼
         ┌─────────────────────────────────────────────────────┐
         │ DM narrates the player's situation + OFFERS OPTIONS  │
         │ Player: pick an option OR type free text             │
         │ DM parses input → proposes ONE effect:               │
         │   setFlag | completeObjective | addKnowledge |       │
         │   addInventory | advanceStep                         │
         │ Engine VALIDATES the proposal against the scenario.  │
         │   accepted → apply (may set a flag a FUTURE step's   │
         │              TriggerCondition reads)                 │
         │   rejected → "No effect: <reason>"                   │
         └─────────────────────────┬───────────────────────────┘
                                   │
                                   ▼
                 Engine advances; loop until all steps terminal
                 → derive win/loss from objective statuses +
                   VictoryConditions → After-Action Review
```

Key invariants:
- The player's response sets a **flag**. Branching is the engine selecting the next step
  whose `TriggerCondition` is satisfied — so a response can change the game **now**
  (next step) or **later** (a deferred step keyed on that flag). The LLM never names the
  branch.
- Free text is parsed by the DM into exactly one validated effect; unparseable or
  invalid input → "No effect" (never silently advances).
- A small condition grammar: `flag:x` / `!x` / `objective:N` / `&&`. Unparseable
  condition → step gated (never silently open).

---

## 4. Backend layout (`services/rpg/`, package `ghosts_rpg`)

```
ghosts_rpg/
  config.py        # settings: GHOSTS_API_URL, OLLAMA_HOST, OLLAMA_MODEL, ports
  loader.py        # 3 GETs → in-memory Scenario model (or JSON-export path)
  models.py        # Scenario, TimelineEvent, Objective, Entity, Edge, GameState
  engine.py        # step pointer, branch selection, condition grammar, validation
  dm.py            # DungeonMaster: narrate + parse free text → ONE proposed effect
  control.py       # Leitung: parse staff products + deterministic umpire findings
  llm.py           # Ollama /api/generate client + OFFLINE deterministic fallback
  scoring.py       # derive win/loss + After-Action Review (deterministic, not LLM)
  session.py       # per-game session store (SQLite/JSON), transcript
  app.py           # FastAPI: REST + WebSocket turn streaming
```

Endpoints (FastAPI):
- `POST /api/games` `{scenarioId}` → load scenario, build GameState, return first turn.
- `GET  /api/games/{id}` → current state (step, options, HUD).
- `POST /api/games/{id}/act` `{input}` → submit option/free text; returns next turn.
- `GET  /api/games/{id}/aar` → After-Action Review when complete.
- `WS   /api/games/{id}/stream` → live DM narration / turn updates.

DM ↔ Ollama mirrors GHOSTS `OllamaConnectorService`: POST `{OLLAMA_HOST}/api/generate`,
env `OLLAMA_HOST` / `OLLAMA_MODEL`, streaming NDJSON. Offline fallback (no host/model)
uses templated narration from `event.Description` + a fixed verb parser.

---

## 5. Frontend (Angular, port 4300)

Terminal-style Zork UI:
- **Scrollback transcript** — green-on-black, DM narration + player input echoed.
- **Command input** — free text; offered options rendered as clickable chips that fill
  the input.
- **HUD** (side rail) — current step/`Time`, your defensive role, objectives with ☑,
  known threat actors, key assets/defenses, active flags.
- **Scorecard** — appears on completion (AAR: grade + grounded highlights).

Talks to FastAPI over REST + WebSocket. CORS `AllowAnyOrigin` for dev.

---

## 6. Build order (vertical slices)

1. **Scaffold** — pyproject `ghosts_rpg`, FastAPI app, config, health check.
2. **Loader + models** — 3 GETs → in-memory Scenario; JSON-export fallback; tests with a
   fake scenario fixture.
3. **Engine** — step pointer, condition grammar, branch selection, proposal validation.
4. **DM offline** — deterministic narrator + verb parser; full game playable with no LLM.
5. **DM Ollama** — wire `/api/generate`; narration + free-text→effect parse; fallback.
6. **Scoring + AAR** — derive win/loss; deterministic scorecard.
7. **Angular UI** — terminal transcript, options chips, HUD, scorecard; WebSocket stream.
8. **Verify live** — against a running Ghosts.Api scenario + in-browser playthrough.

Verification per slice: `pytest` for backend; `ng build` / `ng test` for frontend.
Offline path must stay green with no Ollama and no live API (fixture-driven).
