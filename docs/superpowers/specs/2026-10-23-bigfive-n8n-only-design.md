# Big Five study, n8n-only rebuild

Date: 2026-10-23. Supersedes the 2026-10-20 Python+n8n design and the 2026-10-23 "n8n runs and deploy" direction.
Location: `experimental/bigfive/`.

## Goal

Measure how closely an LLM agent, given one real participant's BFI-2 self-description as its persona, reproduces that
participant's own answers on the 40 Saucier Mini-Markers, and compare models against each other run by run. Everything
an operator touches is an n8n workflow; the store is Baserow; outputs land as files in `experimental/bigfive/out/`.

Success:

- Operator can create the tables, load the two human spreadsheets, add a run for any Ollama or Bedrock model (or upload
  agent answers produced elsewhere), let the run finish, and get `out/<run_id>.html`, `out/<run_id>.csv`,
  `out/all-runs.html`, `out/all-runs.csv`, without leaving n8n.
- After importing a workflow, every Baserow node says in its name which table it reads or writes.
- Re-importing run-001's answers reproduces the existing run-001 numbers (per-facet r 0.73 to 0.89, MAE 0.820, n 438).

Out of scope: GHOSTS NPC creation, prompt variants (fixed `mm-v1`), providers other than Ollama and Bedrock, any Python.

## Shape

| part | job |
|---|---|
| Baserow | nine typed tables in one database, one row per participant per instrument (wide, not long) |
| n8n | five workflows: Create Tables, Import Human Data, Add Run, Run Agents, Analyze and Report |
| disk | `experimental/bigfive/out/` bind-mounted into the n8n container at `/home/node/bigfive-out` |

The Baserow node is used for all row traffic. Its database and table pickers are left empty in the shipped JSON; the
operator picks them once per node after import. Node names follow `<verb>: <table>` with a parenthesised purpose when
one table appears more than once in a workflow (`Read: runs`, `Update: runs (lock)`, `Update: runs (release lock)`).
Each workflow carries a sticky note listing its Baserow nodes and the table each one needs.

Create Tables is the exception: it uses HTTP Request nodes against the Baserow REST API, because the Baserow node cannot
create tables or fields.

## Tables

Every table has a text primary field `key`, unique by construction. Workflows read existing keys before writing and
split rows into batch-create and batch-update, so re-running anything is safe. Number columns are Baserow `number`
fields (3 decimals where fractional); Baserow returns them as strings and every Code node coerces with `Number()`.

| table | key | columns |
|---|---|---|
| `participants` | participant_id | participant_id, age, gender, ethnicity, persona (long text), has_mini_markers (bool) |
| `items` | instrument:item_key | instrument (`bfi2`/`mini_markers`), item_key, position, text, facet, reverse (bool) |
| `human_bfi2` | participant_id | participant_id, bfi1 .. bfi60 |
| `human_mini_markers` | participant_id | participant_id, tda1 .. tda40 |
| `runs` | run_id | run_id, source (`llm`/`import`), provider, model, temperature, prompt_version, batch_size, status (`queued`/`running`/`paused`/`done`), participants_total, participants_done, participants_failed, started_at, last_batch_at, finished_at, locked_at, locked_by, analyzed_at |
| `agent_turns` | run_id:participant_id | run_id, participant_id, provider, model, prompt_version, raw_output (long text), parsed_count, ok (bool), error, duration_ms, answered_at, tda1 .. tda40 |
| `scores` | subject:run_id:participant_id | subject (`human_mini_markers`/`human_bfi2`/`agent`), run_id (empty for human subjects), participant_id, extraversion, agreeableness, conscientiousness, emotional_stability, openness |
| `comparisons` | run_id:participant_id | run_id, participant_id, agent_<facet> ×5, human_<facet> ×5, diff_<facet> ×5, mean_abs_diff |
| `run_summaries` | run_id | run_id, model, provider, n, mean_abs_diff, per facet: shift, mae, r, r_ci_low, r_ci_high, baseline_r (3 decimals) and shift_p, shift_p_holm, steiger_p (6 decimals); analyzed_at |

Date columns take an ISO timestamp or `null`; Baserow rejects `''`.

Scoring, the same as before: facet = mean of its items; a reverse item is `scale_max + 1 - value` (BFI-2 max 5,
Mini-Markers max 9); BFI-2 negative emotionality is reported as `emotional_stability = 6 - NE`. The Mini-Marker keying
(facet and reverse flag per adjective) and the BFI-2 keying live once, in the Import Human Data workflow, which writes
them to `items`; Analyze and Report reads `items` and never hard-codes a facet.

## Workflows

All workflow JSON lives in `experimental/bigfive/workflows/`. Code-node scripts are kept as plain files in
`workflows/src/` and tested with `node --test`; a small `workflows/build.mjs` splices each script into its node so the
committed JSON is always the tested code.

### 1. Big Five 1 - Create Tables

Form: Baserow URL, email, password, database id. Signs in (`POST /api/user/token-auth/`), lists the database's tables,
and for each of the nine tables that does not exist: creates it with a header-only `data` row, then creates the typed
fields (`POST /api/database/fields/table/{id}/`). Tables that already exist are verified column by column; a missing
column is added, a number column whose precision differs is adjusted in place, a type mismatch stops the workflow
with the table and column named. Final node lists each table's name
and id for the operator to pick in the other workflows.

### 2. Big Five 2 - Import Human Data

Form: two file fields, `Personality Data.xlsx` and `Human Mini-Marker Results.xlsx`. Extract From File (xlsx) reads
sheet `Human Responses`, sheet `bfi lookup` and `Sheet1`. One Code node normalizes: 470 participants, the persona text
(`"<item text>: <answer>; ..."` from the lookup), `has_mini_markers` (any tda value present), the 100 `items` rows,
and the two wide response tables. Then for each target table: `Read: <table>` → split by key → `Batch Create: <table>`
and `Batch Update: <table>` (changed values only). Last, human facet scores are computed from the normalized data and
written to `scores` the same way, subjects `human_bfi2` and `human_mini_markers`.

### 3. Big Five 3 - Add Run

Form: run_id, provider (ollama/bedrock), model, temperature (default 0.7), batch_size (default 25), and an optional
file of agent answers (columns `Participant ID`, `tda1..tda40`, the same layout as the human Mini-Marker sheet).

- No file: creates a `runs` row, `source=llm`, `status=queued`. Run Agents takes it from there.
- File: creates the `runs` row with `source=import`, `status=done`, counters filled, and one `agent_turns` row per
  participant with `ok=true` and the 40 values (`raw_output` empty, `provider`/`model` from the form). An existing
  `run_id` is refused with a clear error.

### 4. Big Five 4 - Run Agents

The existing workflow, kept: manual or every 10 minutes; picks the running run or the oldest queued run; lock with
heartbeat and a 30-minute abandonment window; `batch_size` participants that have no turn yet; prompt `mm-v1`;
Bedrock or Ollama chat model by `provider`; parse; write the turn. Changes:

- The 40 parsed values go on the `agent_turns` row as `tda1..tda40`; the `agent_responses` table and its write path
  are removed.
- Baserow nodes renamed to `<verb>: <table>` and left for the operator to pick; the Configuration node keeps only
  `lockMinutes` and `promptVersion`.
- Tables are read whole and filtered in Code nodes (a Baserow node filter would need the field picked from a
  second dropdown); `agent_turns` grows by one row per participant per run, which stays small enough.

Pause and retry semantics are unchanged: set `status=paused` to stop after the current batch; delete a participant's
failed turn rows and set the run back to `queued` to re-ask them.

### 5. Big Five 5 - Analyze and Report

Form: run_id (optional). With a run_id, that run is analyzed; without, every `done` run with no `run_summaries` row.
For each run:

1. `Read: runs`, `Read: items`, `Read: scores`, `Read: agent_turns`, `Read: comparisons`, `Read: run_summaries`
   (whole tables, executed once; filtered in the Analyze Code node).
2. Agent facet scores → `scores` (subject `agent`).
3. Per-participant comparison against `human_mini_markers` scores → `comparisons`. Participants without human
   Mini-Markers are counted but not compared.
4. Summary → `run_summaries`:
   - per facet: mean shift (agent − human), MAE, Pearson r with Fisher 95% CI, paired t-test p for the shift, Holm
     correction across the five facets;
   - BFI-2 baseline per facet: r between `human_bfi2` and `human_mini_markers` scores over the same participants, and
     Steiger's test (dependent correlations sharing the human Mini-Marker variable) for agent r vs baseline r;
   - overall `mean_abs_diff` and n.
5. `runs.analyzed_at` set.

Then the report step reads `run_summaries` and `comparisons` for every analyzed run and writes four files with the
Read/Write Files from Disk node under `/home/node/bigfive-out/`:

- `<run_id>.html`: the per-run report (facts sidebar; agent-vs-human scatter per facet with r and CI; shift chart
  with the largest shift highlighted; parse-quality figure; numbers table).
- `<run_id>.csv`: the `comparisons` rows for the run.
- `all-runs.html`: ranked runs by MAE; per-facet r per run with the BFI-2 baseline as the reference line; shift
  heat table; paired comparison of each run against the best run (paired t on per-participant `mean_abs_diff` over
  participants both runs answered).
- `all-runs.csv`: one row per run from `run_summaries`.

Reports are self-contained HTML with inline SVG and no JavaScript, in the editorial style of the existing run-001
report: light serif display headings, mono eyebrow labels in coral, hairline rules, direct labels instead of legends,
every figure captioned with its finding. Rendering is one Code node per report kind; SVG helpers are shared by being
part of the same script file.

## Statistics in JavaScript

Pearson r, Fisher z CI (`z ± 1.96/√(n−3)`), paired t with a two-sided p from the Student t CDF (regularized
incomplete beta, implemented in the script, no dependency), Holm step-down, Steiger (1980) z for two dependent
correlations with one shared variable, using the agent–BFI-2 correlation as the third r. Each has a unit test with a
known value.

## Deployment

- `src/apphost/AppHost.cs`: add `.WithBindMount("../../experimental/bigfive/out", "/home/node/bigfive-out", isReadOnly: false)`
  and `.WithEnvironment("N8N_RESTRICT_FILE_ACCESS_TO", "/home/node/bigfive-out")` to the n8n container. n8n 2.x blocks
  the Read/Write Files node everywhere unless that variable allows a directory.
- Credentials: the existing `Baserow Token account`, `AWS Bedrock (bigfive)`, and an Ollama credential whose base URL
  is reachable from the n8n container. Credentials are picked in the UI after import; the shipped JSON carries none.
- Import: n8n UI "Import from file" or `n8n import:workflow`. The README is rewritten as the runbook for the five
  workflows and the table picks.
- Human-subject data passes through n8n execution data in the `n8n` Postgres database. The stack is local; the
  spreadsheets stay ignored by git.

## Verification

1. `node --test "experimental/bigfive/workflows/src/*.test.mjs"`: every Code node script plus the statistics.
2. `workflows/build.mjs --check` confirms the committed JSON matches the scripts.
3. Live, against the Aspire stack: Create Tables on database 258 (after removing the old nine tables or on a fresh
   database); Import Human Data with the two spreadsheets (470 participants, 438 with Mini-Markers, 100 items);
   Add Run with run-001's answers, exported to a spreadsheet from the old `agent_responses` table before it is
   removed (`out/run-001.csv` holds facet scores, not answers, so it cannot be used); Analyze and Report; compare the
   new `out/run-001.html` numbers with the current file; then Add Run for a live Bedrock Haiku run with a small batch
   to prove the LLM path end to end.
