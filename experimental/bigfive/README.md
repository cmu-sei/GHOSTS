# Big Five study: humans vs. LLM agents

Measures how faithfully an LLM, handed one real person's BFI-2 self-description as its persona, reproduces that
person's Big Five profile on a second instrument (the 40 Saucier Mini-Markers), and compares LLMs against each other
run by run. Everything an operator touches is an n8n workflow; Baserow is the store; reports land in `out/`.

| part | job |
|---|---|
| **Baserow** | nine typed tables in one database (see [Tables](#tables)) |
| **n8n** | five workflows in `workflows/`: Create Tables, Import Human Data, Add Run, Run Agents, Analyze and Report |
| **`out/`** | `<run_id>.html` + `.csv` per run and `all-runs.html` + `.csv` overall, written by the Analyze workflow |

Design: `docs/superpowers/specs/2026-10-23-bigfive-n8n-only-design.md`.

## Setup

1. **Stack.** The Aspire dev stack (`src/apphost`) runs Baserow, n8n and Postgres. Its AppHost bind-mounts
   `experimental/bigfive/out` into the n8n container at `/home/node/bigfive-out` and sets
   `N8N_RESTRICT_FILE_ACCESS_TO` to that directory, which is what lets the Analyze workflow write the reports.
   On another deployment, reproduce those two settings on the n8n container.
2. **Baserow database.** Create an empty database (Baserow UI, "+ Create new → Database") in your workspace, e.g.
   "Big Five Study", and note its id from the URL (`/database/<id>/`).
3. **n8n credentials.** A **Baserow Token** credential (host as seen from n8n, e.g. `http://baserow`; a database
   token of the workspace owner with create/read/update rights), an **AWS** credential for Bedrock, and an
   **Ollama** credential whose base URL is reachable from the n8n container (Aspire: `http://host.ollama:11434`).
   n8n refuses to activate a workflow with an unset credential, so Run Agents needs both model credentials to
   exist even if you only use one provider.
4. **Import the workflows.** n8n → Workflows → Import from file, one by one, the five
   `workflows/Big Five N - *.json` files. In each imported workflow:
   - a yellow **Tables** note lists every Baserow node and the table it needs. Open each node named there, pick
     your database and the named table from the dropdowns, and select the Baserow Token credential. Every other
     Baserow node in that workflow follows the same table by expression and only needs the credential.
   - the two model nodes in Run Agents need the AWS and Ollama credentials.
   - **Save**, then **Publish/Activate** the form workflows so their production form URLs work
     (`http://<n8n>/form/bigfive-create-tables`, `…/bigfive-import-human-data`, `…/bigfive-add-run`,
     `…/bigfive-analyze`). Unpublished, use "Test workflow" and the test form URL instead.

Node names say what they touch: `Read: runs`, `Batch Create: agent_turns`, `Update: runs (lock)`.

## Runbook

### 1. Big Five 1 - Create Tables

Form: Baserow URL **as seen from the n8n container** (Aspire: `http://baserow`), your Baserow email and password
(database tokens cannot create tables), the database id. Creates the nine tables with typed columns, or, on an
existing database, adds missing columns and fixes number precision; a column of the wrong type stops the run and is
named in the error. Nothing is ever deleted. The output lists table names and ids. Safe to re-run.

### 2. Big Five 2 - Import Human Data

Form: upload `Personality Data.xlsx` (sheets `Human Responses`, `bfi lookup`) and `Human Mini-Marker Results.xlsx`
(`Sheet1`); see `data/README.md`. Writes `participants` (with the BFI-2 persona text), `items` (the instrument
keying), `human_bfi2`, `human_mini_markers` and the human rows of `scores`. Expected with the study data:

```
470 participants (438 with Mini-Markers), 100 items, 908 human score rows
```

Re-running creates nothing twice: rows are matched by `key`, new ones created, changed ones updated. About 15 s.

### 3. Big Five 3 - Add Run

Form: Run ID, Provider (`bedrock` / `ollama`), Model, Temperature (default 0.7), Batch size (default 25), and an
optional **Agent answers** sheet.

- **No file → an LLM run** is queued (`runs.status = queued`); Run Agents picks it up.
- **With a file → an imported run**: the sheet has `Participant ID` and `tda1..tda40` (same layout as the human
  Mini-Marker sheet). The run is stored `done` with one `agent_turns` row per participant and can be analyzed at once.
  Use this for answers produced outside n8n (another harness, an older study).

| field | Bedrock | Ollama |
|---|---|---|
| Model | inference-profile id, e.g. `us.anthropic.claude-haiku-4-5-20251001-v1:0` | tag, e.g. `llama3:70b` |
| Batch size | 100 (≈3 s per participant) | 25 (≈20 s per participant on a 70B model) |

A Run ID that already exists is refused. Runs differ by model; the prompt (`mm-v1`) and the scale are fixed.

### 4. Big Five 4 - Run Agents

Press **Execute workflow** for one batch, or **activate** it: the schedule runs a batch every 10 minutes until the
run is `done`. Each execution picks the `running` run, else the oldest `queued` one, locks it, asks the model for
`batch_size` participants that have no turn yet, writes one `agent_turns` row per participant (raw reply, parse
result, `tda1..tda40` when all 40 adjectives came back exactly once with a value 1–9), refreshes the lock after every
participant, and releases it. Size batches so one finishes inside the 30-minute lock window.

- `runs.participants_done / participants_failed / participants_total` show progress; `locked_by` is the n8n
  execution holding the lock.
- Set `status` to `paused` to stop after the current batch; set it back to `queued` to continue.
- To re-ask participants whose turn failed, delete their `agent_turns` rows and set the run back to `queued`.

### 5. Big Five 5 - Analyze and Report

Form: a Run ID to (re)analyze that run, or empty to analyze every `done` run without a summary yet. Per run it
writes agent rows to `scores`, one `comparisons` row per participant with both an agent answer and human
Mini-Markers, one `run_summaries` row, and stamps `runs.analyzed_at`. Then it writes the files:

| file | content |
|---|---|
| `out/<run_id>.html` | per-run report: agent-vs-human scatter per facet with r and 95% CI, mean-shift chart, parse quality, numbers table |
| `out/<run_id>.csv` | the run's `comparisons` rows |
| `out/all-runs.html` | ranked runs, per-facet r against the BFI-2 baseline, shift heat table, paired comparison of every run against the best |
| `out/all-runs.csv` | one row per run from `run_summaries` |

The two `all-runs` files are rewritten on every execution. Reports are self-contained HTML (inline SVG, no
JavaScript); open them in a browser or print to PDF.

## Tables

Every table has a text primary field `key`, unique by construction; workflows read the existing keys and create or
update accordingly, so re-running anything is safe. Baserow returns numbers as strings; the Code nodes coerce them.

| table | key | holds |
|---|---|---|
| `participants` | participant_id | demographics, the BFI-2 persona text, `has_mini_markers` |
| `items` | instrument:item_key | the 60 BFI-2 and 40 Mini-Marker items with facet and reverse flag |
| `human_bfi2` | participant_id | bfi1..bfi60 |
| `human_mini_markers` | participant_id | tda1..tda40 (only participants who answered) |
| `runs` | run_id | source (`llm`/`import`), provider, model, temperature, prompt_version, batch_size, status, counters, lock, analyzed_at |
| `agent_turns` | run_id:participant_id | one row per LLM call or imported answer: raw_output, parsed_count, ok, error, duration_ms, tda1..tda40 |
| `scores` | subject:run_id:participant_id | facet means; subjects `human_mini_markers`, `human_bfi2` (run_id empty) and `agent` |
| `comparisons` | run_id:participant_id | agent, human and agent−human per facet, mean_abs_diff |
| `run_summaries` | run_id | n, mean_abs_diff; per facet: shift, shift_p, shift_p_holm, mae, r, r_ci_low, r_ci_high, baseline_r, steiger_p |

**Scoring.** facet = mean of its items; reverse items are `scale_max + 1 − value`; BFI-2 negative emotionality is
reported as `emotional_stability = 6 − NE`. The keying is defined once, in `workflows/src/normalize_humans.js`, and
written to `items`; Analyze reads it from there.

**Statistics** (per facet, per run): mean shift agent−human with a paired t-test p, Holm-corrected across the five
facets; MAE; Pearson r with a Fisher 95% CI; the *BFI-2 baseline* r (how well each person's own BFI-2 predicts their
Mini-Markers, same participants) and Steiger's test for the agent's r against it (dependent correlations sharing the
human Mini-Marker score). The all-runs report adds a paired t-test of each run's per-participant mean_abs_diff
against the best run. p-values are stored with 6 decimals.

## Development

Workflow JSON is generated, not hand-edited:

```
workflows/
  defs/*.mjs        one module per workflow: nodes, connections, sticky notes
  lib/n8n.mjs       node factories (Baserow read/create/update, upsert block, form, loop …)
  src/*.js          the Code-node scripts, exactly as they run in n8n
  src/lib/*.js      shared code spliced into scripts by `// @include lib/x.js`
  build.mjs         defs + src -> "Big Five N - *.json"   (--check: fail if the JSON is stale)
```

```bash
cd experimental/bigfive
node workflows/build.mjs                        # regenerate the five JSON files
node workflows/build.mjs --check                # CI-style staleness check
node --test "workflows/src/*.test.mjs"          # 23 tests: scoring, statistics, every Code node, create_tables against a fake Baserow
```

The scoring test also replays the shipped `out/run-001.csv` human facet scores when the normalized CSVs are present.

**Changing the prompt:** edit `workflows/src/build_prompt.js`, bump the version in its header comment and in
`workflows/src/configuration.js`, rebuild, re-import Run Agents. Run Agents refuses a run whose `prompt_version`
differs from its own, so old runs are never silently re-asked with a new prompt.

## Troubleshooting

- **Importing leaves Baserow nodes red** — expected: pick the database and table on the nodes listed in the
  yellow Tables note, and the credential on every Baserow node.
- **"Cannot publish workflow: Missing required credential: ollamaApi"** — create an Ollama credential (any URL) even
  if you only run Bedrock; n8n validates every node before activating.
- **Create Tables: "Column type mismatch … nothing was deleted"** — a table with that name exists with a different
  column type. Rename it or move it; the workflow never alters types.
- **Write: out files: "Access to the file is not allowed"** — the n8n container lacks
  `N8N_RESTRICT_FILE_ACCESS_TO=/home/node/bigfive-out` (and the bind mount). Both are set in `src/apphost/AppHost.cs`.
- **Run Agents: Pick run outputs nothing** — no run is `queued`/`running`, or its lock is younger than 30 minutes
  (another batch is mid-flight, or a crashed one; the lock expires on its own).
- **Every turn fails with `llm: …security token…`** — the AWS credential is stale. Fix it, delete the failed
  `agent_turns` rows, set the run back to `queued`.
- **Every turn fails with `missing: …`** — the model ignores the answer format. Read `raw_output` on a failed turn;
  a lower temperature usually fixes it. Failed turns never pollute scores.
- **Analyze: "Nothing to analyze"** — no `done` run lacks a summary; give a Run ID to re-analyze one.
