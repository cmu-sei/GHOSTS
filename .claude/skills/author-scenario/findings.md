# Handling findings

The rule is the design's, in §4a:

> **the agent repairs mechanical findings but escalates judgment ones** — a dangling reference it fixes
> and re-validates; a finding that implies a mission decision goes back to the developer. An agent that
> quietly resolves the second kind has rebuilt the original problem, only faster.

So every finding gets sorted into exactly one of three piles, and the sort is visible to the developer:

1. **Mechanical** — the document contradicts itself or the schema. There is one right answer and it does
   not depend on what the exercise is for. **Fix it, say what you changed in one line, re-validate.**
2. **Judgment** — the finding is a mission decision surfacing as a defect. **Take it back to the
   developer with the question it implies**, which is usually one of the fourteen in
   `../../../schemas/scenario-document/ELICITATION.md`.
3. **Accepted** — a warning or note that is correct about the document and correct about the exercise.
   **Say why it is acceptable, in a sentence, at step e.** An unexplained warning is an unmade decision.

A finding shape is `tier` (1–4), `severity` (`error` blocks import; `warning` and `info` do not), a
stable `code`, a JSON pointer `path` into the document, a `message`, and often a `hint`. The hint is
usually the repair; read it before inventing one.

## Never do this

- **Do not delete the thing the finding points at** to make the finding go away. An `ATTACK_UNKNOWN_TECHNIQUE`
  is fixed by looking the technique up, not by dropping the id. A `TIME_OUTSIDE_DURATION` is fixed by
  correcting the offset or the duration — and which one is correct is sometimes a question.
- **Do not widen the duration to fit a stray offset** without saying so. That silently rewrites E1.
- **Do not pick a placeholder value to reach zero errors** and intend to ask later. Zero errors is a
  claim that the document is ready; a placeholder makes it a false one.
- **Do not suppress a `revoked` or `deprecated` flag.** MITRE retired the technique; pick another.
- **Do not hand-fix anything in the database.** The document is the source; import is the only write.

## By code

### Mechanical — fix and re-validate

| Code | Tier | Severity | The repair |
|---|---|---|---|
| `SCHEMA_*` | 1 | error | The schema said what it wanted; the message and hint name the keyword. `SCHEMA_ADDITIONAL_PROPERTIES` is usually a field you invented or misspelled — check `schemas/scenario-document/v1/`, do not guess. |
| `SCHEMA_NOT_AN_OBJECT`, `SCHEMA_VERSION_MISSING`, `SCHEMA_VERSION_UNSUPPORTED` | 1 | error | The file is not a scenario document or declares a version the API does not support. Set `schemaVersion` to `1.1.0`. |
| `ID_DUPLICATE` | 2 | error | Two things share an id. Rename the later one and update every reference to it. |
| `REF_UNKNOWN_ENTITY`, `REF_UNKNOWN_OBJECTIVE`, `REF_UNKNOWN_SEGMENT`, `REF_UNKNOWN_WORKFLOW`, `REF_UNKNOWN_REFERENCE` | 2 | error | A pointer to something the document does not declare — a typo, or a declaration you forgot. The hint lists what *is* declared. |
| `REF_UNKNOWN_HOST` (error, at `/terrain/vulnerabilities/*/asset` or `/terrain/defenses/*/covers/*`) | 2 | error | A vulnerability or defense on a host that does not exist. Declare the host or fix the name. |
| `REF_OBJECTIVE_SELF_PARENT` | 2 | error | An objective is its own parent. Drop the `parentId` or point it at the real parent. |
| `ATTACK_UNKNOWN_TECHNIQUE` | 2 | error | The id is not in the committed index. **Call `attack_technique_lookup`** and use what comes back. This finding almost always means an id came from memory. |
| `ATTACK_REVOKED_TECHNIQUE` | 2 | error | MITRE revoked it and named a replacement. Look up the replacement. |
| `TIME_DURATION_NOT_POSITIVE` | 2 | error | `rulesOfPlay.duration` does not parse to a positive span. |
| `TIME_OUTSIDE_DURATION` | 2 | error | An offset falls outside the exercise. Mechanical *only when the arithmetic is wrong* — see below. |
| `DRYRUN_LOAD_FAILED` | 4 | error | The document is valid but the loader could not build it. The message names the field. |
| `REF_UNKNOWN_HOST` (warning, at `/timeline/events/*/description`) | 2 | warning | An event's prose names a host the terrain does not declare. Either declare it or fix the prose — prose that names non-existent hosts is how a plan and a document drift apart. |
| `FLAG_NEVER_READ` | 2 | warning | A flag is set and nothing reads it: usually a branch you meant to write, or a leftover. Write the condition or remove the flag. |
| `DRYRUN_COMPILE_FAILED`, `DRYRUN_COMPILE_INCOMPLETE` | 4 | warning | The timeline did not compile to handlers, or compiled partly. Fix what the message names. |

### Judgment — escalate with the question

| Code | Tier | Severity | The question it implies |
|---|---|---|---|
| `TIME_DURATION_NOT_STORABLE` | 2 | error | GHOSTS stores one duration, in whole hours, so a sub-hour exercise cannot be imported. **Ask E1**: is this exercise really 45 minutes, or is it an hour? Do not round it to make the import work — that changes what the developer said the exercise is. If the answer is "really 45 minutes", the document is right and the import refuses; say so plainly and stop. (Schema README open question 5.) |
| `TIME_OUTSIDE_DURATION` | 2 | error | Mechanical when you mis-converted a date. **Judgment when both numbers are intentional** — an inject scheduled after the exercise ends is either a wrong inject time or a wrong duration, and which one gives way is E1 or B3. Ask; do not pick the cheaper edit. |
| `ATTACK_DEPRECATED_TECHNIQUE` | 2 | warning | MITRE deprecated it without a replacement. **Ask D2**: is the deprecated technique the point (this adversary really does the old thing), or should the adversary press differently? |
| `REF_UNSET_FLAG` | 2 | warning | A condition reads a flag nothing sets, so the branch is false until something outside the document raises it. **Either a typo (mechanical) or an adjudicated outcome (judgment)** — if a white cell raises it, that is B3, and it belongs in `decisions.md` as adjudicated, with who adjudicates named. Never silence it by adding a `setFlags` you invented; that quietly removes a human from the exercise. |
| `DRYRUN_NO_NPCS` | 4 | warning | The document populates no NPCs, so nothing will act. **Ask A3**: who is simulated and who is played? |
| `TERRAIN_UNSTRUCTURED` | 2 | info | Terrain is prose, not hosts and segments, so nothing downstream can check it. **Ask E2** for the slice and what this exercise adds, then structure it. This is an info finding you should almost never accept. |
| `TIME_NO_DURATION` | 2 | info | No duration declared, so no offset can be checked. **Ask E1.** |

### Usually accepted — say why

| Code | Tier | Severity | Why it is often fine |
|---|---|---|---|
| `DRYRUN_LOADED`, `DRYRUN_COMPILED`, `DRYRUN_READINESS` | 4 | info | The dry run's own report: what loaded, what compiled, how ready the scenario is. Quote the readiness note to the developer; it is the closest thing to "this will actually run". |
| `WORKFLOW_NOT_REGISTERED` | 2 | warning | A workflow the document names is not in n8n *yet*. Fine while authoring, not fine before running. Say which ones. |
| `WORKFLOW_CHECK_SKIPPED` | 2 | info | n8n was not reachable, so workflow refs went unchecked. Note it; it is not a defect in the document. |

## A code not in this table

Sort it by the same test, and say which pile you put it in and why: **could a machine with no knowledge of
what this exercise is for produce the one right answer?** Yes → mechanical. No → judgment. Then add it
here.
