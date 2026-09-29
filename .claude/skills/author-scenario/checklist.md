# The interview

**The questions live in [`schemas/scenario-document/ELICITATION.md`](../../../schemas/scenario-document/ELICITATION.md).
Read that file. Do not work from this summary of it, and do not paraphrase the questions from memory.**

It is in the repository rather than in the skill on purpose: it is derived from the schema and from the
two Step 0 runs, it is cited from the schema README, and the validator's judgment findings point at it.
A copy here would be a second source of truth that drifts.

Fourteen questions, under the design's five headings:

| | |
|---|---|
| **A** — the training audience and its proficiency | A1 who is in the room · A2 what they can already do · A3 who is simulated and who is played |
| **B** — the decisions the audience must be forced to make | B1 what decision, by when · B2 what makes it correct · B3 what puts it in front of them, and who adjudicates |
| **C** — the end state that counts as success | C1 how far the adversary gets, and the last recoverable rung · C2 what happens on early success |
| **D** — what may fail and what may not | D1 rules of engagement · D2 how hard the adversary presses · D3 what must be visible, and any intended blindness |
| **E** — hard constraints | E1 duration and pacing · E2 terrain base, additions, off-limits · E3 real or fictional adversary |

Each entry in ELICITATION.md gives the question in the developer's words, the document paths its answer
fills, why no default is defensible, and **whether you may propose an answer or must ask**. Honour that
last field: where it says *must ask*, a proposal is not a substitute, even a well-reasoned one.

## How to ask

- **Only what the intent leaves open.** An intent that says "a nine-person utility SOC, first rotation"
  has answered A1 and A2. Asking again is noise, and the interview is measured by how few questions
  reach a validated draft.
- **First, say what you are not asking.** List the questions the intent answered and quote the line that
  answered each. That is the developer's check that you read it, and it is where they catch a
  misreading cheaply.
- **One batch** for everything independent. Most of the fourteen are independent; the exceptions are
  B2 (needs B1's decision), C1's last-recoverable rung (needs the ladder's endpoints from the intent or
  C1's first half) and D2's capability (needs the objective or the ladder). Ask those in a second batch
  if the first batch's answers change them.
- **Number them by tag** — A1, B3, E2 — so the developer can answer in any order, skip, and see which of
  the fourteen were skipped.
- **Ask the question, not the field.** "Which rung is the last one where recovery is still possible?"
  not "what value for `rulesOfPlay.escalationLadder.rungs[].recoverable`?". The developer knows their
  exercise, not the schema.

## "You decide"

An allowed answer to any question, including a *must ask* one. When you get it:

1. Propose a value.
2. Give **one sentence** of reasoning — why this value and not the obvious alternative.
3. Record it in `decisions.md` as **`proposed`**, never as the developer's.
4. Do not ask a follow-up. "You decide" is a decision about who decides; re-asking overrides it.

The distinction between `stated` and `proposed` is the ledger's entire purpose. A reviewer reading it
must be able to tell which mission judgments a person made and which a model made — that is the
difference between this and the Step 0 runs, where sixteen judgments were settled by default and nobody
could see afterwards which.

## Everything else

Everything not among the fourteen you **propose with one sentence of reasoning, for the developer to
accept in a word**. ELICITATION.md's closing section lists what that covers — the slug and name,
`context.situation`, event titles and descriptions and indicators, the entity graph,
`startingConditions`, the information environment, performance metrics, workflows, sources — and the
three things that are deliberately never questions: ATT&CK ids (look them up), clock arithmetic (compute
it), and anything the API discards (proposing it costs nothing; asking wastes one of fourteen).
