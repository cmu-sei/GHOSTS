# GHOSTS RPG — Scenario Player (Zork Mode)

A single-player, Zork-style web game for cyber/cogwar mission rehearsal, built on
GHOSTS scenarios. The computer is the **Dungeon Master** and exercise-control cell:
it narrates, plays everyone except the player (including the OPFOR), and adjudicates
your staff products. You hold the **Blue Team** (defensive) seat and submit estimates,
orders, or terse commands; your responses bend the timeline forward.

See [DESIGN.md](DESIGN.md) for the architecture.

## Run it

**Backend** (FastAPI, port 8095):

```bash
cd services/rpg
python3 -m venv .venv && . .venv/bin/activate
pip install -e ".[dev]"
python -m uvicorn ghosts_rpg.app:app --host 127.0.0.1 --port 8095
```

**Frontend** (Angular, port 4300):

```bash
cd services/rpg/frontend
npm install
npx ng serve --port 4300
```

Then open <http://localhost:4300>.

### Play in the terminal (no UI)

```bash
cd services/rpg && . .venv/bin/activate
python -m ghosts_rpg.play          # bundled phishing-drill scenario
python -m ghosts_rpg.play path/to/bundle.json
```

## Data source

A game loads one `ScenarioBundle`, from either:

- a **fixture export** under `fixtures/scenarios/*.json` (offline, default), or
- a **live GHOSTS scenario** via three GETs (`/api/scenarios/{id}`,
  `/builder/graph`, `/objectives?scenarioId=`). Set `GHOSTS_API_URL` and POST
  `/api/games` with `{"scenarioId": N}`.

## DM brain

Narration uses Ollama (`OLLAMA_HOST` / `OLLAMA_MODEL`, mirroring the GHOSTS API). With
no model configured, a deterministic offline narrator/parser keeps the game fully
playable. Player input parsing always runs through the deterministic intent map, so
actions map to legal effects with or without a model.

## Kriegsspiel mode

The same terminal UI now accepts staff-product style input:

```text
task 4: Priority: ransomware precursor
Plan: isolate FIN-WS-04 and kill the shadow-copy deletion
Assumptions: EDR isolation works; SOC has authority
Information Requests: confirm SMB sessions to FS01
Risk: VPN lockout waits
```

Exercise control (`Leitung`) parses the product, records assumptions, checks known
constraints, emits umpire findings, and then lets the engine apply only validated
effects. Short commands such as `isolate the host` still work.

## Test

```bash
cd services/rpg && . .venv/bin/activate && pytest      # backend (28 tests)
cd services/rpg/frontend && npx ng build               # frontend
```
