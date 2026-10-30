# Scenario Builder (experimental)

A standalone, guided scenario builder: a .NET API that owns the question ledger, an Angular
workspace, and a Pi agent in a side panel that walks a designer through a kind's questions on a
local or frontier model. It is separate from GHOSTS; GHOSTS and others pull the finished JSON.

- Design: [docs/specs/2026-10-29-scenario-builder-design.md](docs/specs/2026-10-29-scenario-builder-design.md)
- Plan for this first piece: [docs/plans/2026-10-29-first-piece.md](docs/plans/2026-10-29-first-piece.md)

| Folder | What |
|---|---|
| `api/` | .NET 10 minimal API: kinds, ledger, gates, proposals, log, pull API, model pass-through |
| `agent/` | TypeScript agent (Pi SDK 1.4): core, browser bundle, evaluation |
| `ui/` | Angular 22 workspace with the agent panel |
| `kinds/` | Kind files; `crucible-tabletop.kind.json` is stages 0–3 of a Crucible tabletop |
| `schema/` | JSON Schema of what `GET /api/scenarios/{id}` returns |
| `tools/` | LiteLLM config for Sonnet on Bedrock |

## Run it

```bash
# API on :5280. Its SQLite file is created at start; delete it to reset everything.
cd api/ScenarioBuilder.Api && dotnet run

# UI on :4310 (proxies /api to :5280). Builds the agent bundle first.
cd ui && npm start
```

Open http://localhost:4310, create a scenario, and press **Start with the agent**, or answer on
the Questions page by hand.

**Installing packages:** `npm install` can fail with `ENOTDIR` on this devcontainer's mount.
Install in a container-local folder and copy `node_modules` back:

```bash
mkdir -p /tmp/sb-agent && cp agent/package*.json /tmp/sb-agent && (cd /tmp/sb-agent && npm ci) && cp -a /tmp/sb-agent/node_modules agent/
```

## Models

The API is the only path to a model (`ModelServers` in `api/ScenarioBuilder.Api/appsettings.json`);
keys stay on the server.

- **gemma4:26b** on the Mac's Ollama at `192.168.65.254:11434`. Ollama must give it at least a 32k
  context, or it silently drops the start of the conversation:

  ```bash
  curl http://192.168.65.254:11434/api/pull   -d '{"model":"gemma4:26b"}'
  curl http://192.168.65.254:11434/api/create -d '{"model":"gemma4:26b-32k","from":"gemma4:26b","parameters":{"num_ctx":32768}}'
  # then point the gemma4:26b entry's "Model" at gemma4:26b-32k
  ```

- **sonnet-5.5** through LiteLLM on :4000, with the AWS credentials from the environment:

  ```bash
  export LITELLM_KEY=sk-local-$(openssl rand -hex 8)    # the API reads it too
  docker run -d --name sb-litellm -p 4000:4000 \
    -v "$PWD/tools/litellm.yaml:/app/config.yaml:ro" \
    -e LITELLM_MASTER_KEY="$LITELLM_KEY" -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY -e AWS_SESSION_TOKEN -e AWS_REGION \
    ghcr.io/berriai/litellm:main-stable --config /app/config.yaml
  ```

- **fake**: `node agent/evals/fake-model.ts` on :5299, a rule-driven stand-in for plumbing checks.

## Tests

```bash
dotnet test api/ScenarioBuilder.slnx                  # API rules
cd agent && npx tsc --noEmit && npx vitest run         # agent, tools, checks, harness
cd ui && npx ng test --watch=false && npm run build   # UI
node ui/e2e/smoke.mjs                                  # pages + panel in headless Chromium (API, fake model, ng serve up)
```

## The evaluation

The question this first piece answers: can gemma-4-26b walk a designer through the stage 0–3
questions as well as Sonnet 5.5? A scripted designer answers from `agent/evals/fixtures/harbor-blackout.json`
(a fictional port outage, no adversary), and ten structural checks score each run.

```bash
cd agent && npm run eval -- --models gemma4:26b,sonnet-5.5 --repeat 3
```

It writes `agent/evals/results/report.md`. **Pass:** gemma passes all ten checks in at least two
of three runs, and no check passes on Sonnet while failing on gemma in all three.
