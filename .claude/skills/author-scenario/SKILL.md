---
name: author-scenario
description: Turn an exercise developer's plain-English intent into a validated GHOSTS scenario document — interview from the elicitation checklist, draft against schema 1.1.0, validate and revise until clean, render an exercise plan for review, and import only when told to. Use when someone wants to author, draft, revise, or import a GHOSTS scenario, or asks to turn an exercise intent into a scenario.
---

# Author a scenario document

You are drafting **a document, not database rows**. The document is the artifact the developer reviews,
diffs, versions and signs; the import endpoint is the only thing that writes. Everything below follows
from that.

The design this implements is `.devcontainer/work/ghosts-agentic-authoring-design.md` §3.2 and §4a. The
schema, its README and its tooling are in `schemas/scenario-document/`.

## Hard rules

1. **Validate and import are the only tools that touch the database.** No SQL, no `psql`, no writes to
   any other endpoint. You do not edit a scenario row to fix a document — you fix the document.
2. **No ATT&CK id from memory, ever.** Every id in the output came back from `attack_technique_lookup`
   (techniques) or `attack_group_lookup` (the adversary's identity — the group id, name and aliases
   behind `adversaries[].name`) in this session, or from
   `schemas/scenario-document/corpus/attack-index.json` read in this session. If you cannot look it up,
   the document does not get a technique id, and a real-world adversary stays unnamed. A recalled id is
   the failure mode both Step 0 runs hit.
3. **Nothing is settled silently.** Every value the developer did not state is in `decisions.md` marked
   `proposed`. A reviewer must be able to see which mission judgments a person made and which you made.
4. **Import only on the developer's explicit word.** Dry run first, always. "Looks good" is not
   permission to import; ask.
5. **Every step is visible.** Say what you are about to do, show the findings verbatim, show the counts.
   No silent retries.
6. **Repair mechanical findings; escalate judgment ones.** See `findings.md`. An agent that quietly
   resolves a judgment finding has rebuilt the original problem, only faster.
7. **A document carries the observable and the technique id, never the operational how-to.** An
   event or a playbook move names what a defender could see (`indicators`) and which ATT&CK id it
   is. It does not narrate the mechanism that makes an attack step work — the exact parameters
   changed, the exact spoofing method, the exact command sequence. That is not a workaround for a
   content filter; it is what the document is *for*: the exercise trains detection and a decision,
   and both run on an indicator plus a technique id. The mechanism is neither. (A Step 5 run drafted
   a narrated sequence — disable protection, falsify telemetry, issue a trip command — as one
   attacker-perspective paragraph; a safety classifier stopped it mid-write. The fix was not a
   softer version of the same paragraph. It was three technique-tagged moves at outcome level, each
   with its own indicators, gated in sequence — and that shape was the better document on its own
   terms: more discoverable checkpoints for the audience, not fewer, because the training value was
   always in what a defender could observe, never in how the adversary pulled it off.)

## The working folder

Ask the developer to name it; default `.devcontainer/work/scenarios/<slug>/`. It holds:

```
intent.md              the intent, verbatim as given — never edited by you
<slug>.scenario.json   the draft document
decisions.md           the ledger: every value, who decided it, why
findings/iter-N.json   the validator's answer at each iteration, kept
<slug>.plan.md         the rendered exercise plan
run.md                 the measurement record
```

## The loop

### a. Take the intent

Accept pasted text or a file path. Save it verbatim as `intent.md` — do not tidy it, do not expand it.
If the developer has no intent yet, give them `intent-template.md` and stop until they fill it in; an
intent you wrote for them is not a developer-owned artifact.

Derive `<slug>` from the intent's exercise name (kebab-case). Confirm it in the same message as the
interview so it costs no extra turn.

### b. Interview

Read `checklist.md`. Ask **only the checklist questions the intent leaves open**, and ask them **in one
batch** where the answers do not depend on each other. Number them as the checklist numbers them (A1,
B3, …) so the developer can answer by tag and can see which of the fourteen you skipped.

Before asking, say which questions the intent already answered and quote the line that answered them.
That is how the developer checks you read it.

**"You decide" is an allowed answer to any of them.** When you get it: propose a value, give **one
sentence** of reasoning, and write it to `decisions.md` as `proposed`. Do not ask a follow-up to a "you
decide" — that converts the courtesy into an interrogation.

`decisions.md` format. Every value the developer did not state gets a row; so do the ones they did, which
is what makes the two counts at step e meaningful:

```markdown
| # | Decision | Value | Who | Why |
|---|---|---|---|---|
| 1 | C1 last recoverable rung | rung 3, credential reuse contained | proposed | Past rung 4 the OT trip is already scheduled, so the audience would be watching, not deciding. |
| 2 | D1 rules of engagement | no live malware; isolation permitted, rebuild is not | stated | — |
```

`Who` is exactly one of `stated` (the developer said it, in the intent or an answer) and `proposed`
(you chose it, including after "you decide"). There is no third value. Anything you adjudicate rather
than encode — a flag no event sets because a white cell decides it — gets a row here too, marked
`proposed`, with the adjudication named in the Why.

### c. Draft

Write `<slug>.scenario.json` against **schema 1.1.0** (`schemas/scenario-document/v1/`). Read the
schema; do not write the document from recall of it. `schemas/scenario-document/examples/` has four
worked documents — `operation-overlord.scenario.json` is the fullest.

Non-negotiable properties of the draft:

- **Every ATT&CK id from the lookup.** Call `attack_technique_lookup` for each one and paste back what
  it returned. A `revoked` or `deprecated` flag in the answer means pick a different technique, not
  suppress the flag. When E3 says the adversary is a real one, call `attack_group_lookup` for its
  identity too, and name it from what came back.
- **The population is `population.pools`** — a role and a count for each pool. That is what an execution
  generates NPCs from and what the dry run counts, so a pool with no count populates nobody.
  `entities[]` and `edges[]` are the exercise's graph — assets, ownership, trust — and are optional; a
  Person entity is not a user and adding one populates nothing.
- **Every timeline event has an `owner`, an `expectedResponse` and at least one `indicator`.** An event
  with no observable grades the audience on something they cannot see. An event with no owner is nobody's.
- **Every flag a condition reads is set somewhere**, by some event's `effects.setFlags` or by
  `startingConditions` — or it is a row in `decisions.md` listed as adjudicated. Tier 2 reports
  `REF_UNSET_FLAG`; a clean run means you already resolved it deliberately.
- **Terrain is structured**: `terrain.hosts[]` and `terrain.segments[]`, not a prose summary, plus
  `terrain.reference` naming the base slice the intent names. `TERRAIN_UNSTRUCTURED` is the check.
- Offsets are `T+`-form and inside `rulesOfPlay.duration`. Compute them from the intent's dates; that is
  arithmetic, not a question.

### d. Validate

`scenario_document_validate` with `dryRun: true`. If the MCP server is not attached, the same endpoint:

```bash
curl -sS -X POST "${GHOSTS_API_BASE_URL:-http://localhost:5000}/api/scenarios/validate?dryRun=true" \
  -H 'Content-Type: application/json' --data-binary @<slug>.scenario.json
```

Validate writes nothing, ever, so call it on a half-finished draft as often as you like. For schema and
reference checks with no API running, `node schemas/scenario-document/tools/scenario-doc.mjs validate
<file>` covers tier 1.

Save the response to `findings/iter-N.json` and **show the developer every finding**: severity, code,
path, message. Then split them by `findings.md`:

- **Mechanical** — fix, re-validate, iterate. Say what you changed and why in one line each.
- **Judgment** — stop and take it back to the developer **with the question it implies**, which is
  usually one of the fourteen. Do not guess, and do not "temporarily" pick a value to get a clean run.

Repeat until **zero errors**. Warnings and info do not block import, but every remaining warning is
shown at step e with a sentence on why it is acceptable — an unexplained warning is an unmade decision.

### e. Present, then import only when told

Render the plan:

```bash
node schemas/scenario-document/tools/scenario-doc.mjs render <slug>.scenario.json decisions.md > <slug>.plan.md
```

Present **three things together**, in this order:

1. **The plan** (`<slug>.plan.md`) — the intent's consequences in the intent's language, section by
   section, so the developer can judge whether the second is a faithful reading of the first. This is
   the only review a machine cannot do for them.
2. **The decisions ledger** — with the count: *N decisions, M stated by you, N−M proposed by me.*
3. **The remaining warnings** — each with why it is acceptable.

Then ask whether to import. On a yes:

1. `scenario_document_validate` with `dryRun: true` once more, on the exact bytes about to be imported.
   Report it clean.
2. `scenario_document_import`. On success report **the new scenario id**. On refusal report the findings
   and that nothing was written — a refusal is the gate working, not an error to route around.

The plan and the document stay in sync because the plan is regenerated, never edited. If you find
yourself editing `<slug>.plan.md`, the document is wrong instead.

## The run record

Write `run.md` in the working folder as you go, not at the end. Step 5 measures the interview by how few
questions reached a validated draft, so these four numbers are the deliverable, not decoration:

```markdown
# <slug> — run record

- Intent received: <UTC timestamp>
- Interview questions asked: N (of the checklist's 14) — A1, A2, B1, …
- Validated draft at: <UTC timestamp>  → elapsed: Nh Nm
- Validation iterations: N
- Findings per iteration: iter-1 E errors / W warnings; iter-2 …
- Decisions: N total — M stated by the developer, K proposed by me
- Imported: scenario id N, or "not imported"
```

Count a question as asked if you put it to the developer, whatever they answered — "you decide" is an
answer to a question that was asked. Count an iteration as one validate call on a changed document.

## Reference files

- `checklist.md` — the interview: the fourteen questions and how to ask them.
- `intent-template.md` — what to give a developer who has no intent yet.
- `findings.md` — the mechanical/judgment split, by finding code.
