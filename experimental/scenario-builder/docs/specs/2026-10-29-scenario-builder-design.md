# Scenario Builder: design

**Date:** 2026-10-29 · **Status:** for review · **Folder:** `experimental/scenario-builder/`

This is the design for a standalone scenario builder, separate from GHOSTS, and for the first
piece of it to build. Section 2 records what was decided in the design interview on 2026-10-29.
Section 4 describes the whole product so that the first piece points the right way. Sections 5
to 15 cover the first piece only, and section 16 lists what comes after it.

## 1. Why

Several teams build exercise scenarios, from tabletop exercises (TTX) to range exercises. Today a
good scenario needs an engineer, a configured devcontainer, Claude Code and its skills, and days of
back-and-forth. Exercise Tidal Moon, a Crucible tabletop built that way, is one example of the
quality we're after. It's a use case, not part of the builder. Its own team says most of their ten days went on design decisions that a guided tool could have asked
about on day one (`reference/tidal moon artifacts/ttx-scenario-builder-design-input.md`, from here
on "the Monsoon design input").

Many customers can't or won't use frontier models, because of cost or policy, and some deployments
are air-gapped ranges with only local models. Jeffort (`reference/jeffort.zip`) shows that an agent
built on the Pi agent SDK can work well on gemma-4-26b when its tools do the hard work.

The builder's job: **walk someone who is not an engineer through the questions that lead to a very
good scenario, keep every decision and artifact consistent, and work on a local model.**

## 2. Decisions from the interview

| # | Topic | Decision |
|---|---|---|
| 1 | Product | A standalone scenario builder. It's not a GHOSTS feature. GHOSTS, Crucible and others are consumers. |
| 2 | Modules | The scenario builder and a network (environment) builder are separate. A scenario can copy a network design in or point to a saved one. The network builder is out of scope for now. |
| 3 | Output | One exercise document, the "full scenario JSON". Everything else (PDFs, the MSEL, the Crucible import file) is rendered from it. |
| 4 | Renderers | Code adapters for machine targets. Team-supplied templates for documents people read. Designing templates is its own path. The agent may write one-off drafts but can't create templates. |
| 5 | Who drives the interview | The tool keeps the question ledger (open, answered, delegated). The model chooses wording and order, and nothing counts as answered until a tool records it. |
| 6 | Agent authority | The agent edits delegated items and craft (wording, layout) directly, logged and revertible. A change to anything the person stated must go through a proposal. |
| 7 | Collaboration | Several people with their own agents on different parts of one draft. Conflicts are handled per element through versions. There's no live co-editing of the same text. |
| 8 | Approval | One owner per scenario approves every proposal that touches a stated decision. |
| 9 | Scope | Author only, with results flowing back read-only later. The first version deploys nothing: consumers pull the full JSON from an API. |
| 10 | Main screen | A structured workspace with the agent in a side panel, and a Questions page as the spine. Nobody starts at an empty prompt. |
| 11 | Kinds | A kind (for example "Crucible tabletop") is a data file: its stages, questions, gates, checks and outputs. We write the kinds for now. |
| 12 | Checks | Make checks structural wherever possible, and have the model flag what's left. Each check is a stop or a warning, set per kind and per project. There's no hard floor for now. Stops block sign-off only. Export is never blocked; it carries its open findings. |
| 13 | Libraries | Shared items (scales, vocabulary, organizations, network designs) are pinned by version. When a newer version exists, the builder offers it with a preview of what changes. Pinned items are copied into the pulled JSON. (A working default; nobody objected.) |
| 14 | Where the agent runs | The conversation runs in the browser. Long jobs run in a server-side worker on the same agent code. The model is always reached through the builder's server. |
| 15 | Model floor | An OPAL-class gateway serving gemma-4-26b. For development, `gemma4:26b` runs under Ollama on a 64 GB M1 Pro. A frontier model is used where one is available. |
| 16 | Stack | C# (.NET 10) API and an Angular front end, shaped like jeffort. A TypeScript agent on the Pi SDK. |
| 17 | First piece | The Crucible tabletop kind, stages 0 to 3, scored on gemma against Sonnet before anything else is built (section 5). |

## 3. Words used here

| Word | Meaning |
|---|---|
| Kind | A type of scenario, defined by a data file: stages, questions, gates, checks, outputs. |
| Stage | A group of questions, answered before the next group. From the Monsoon design input. |
| Gate | What must be true before the next stage opens, for example "the owner approved the executive summary". |
| Ledger | The record of every question: open, answered, or delegated. For each answer it records who decided it, when, and why. |
| Stated | An answer a person gave. Only a proposal can change it. |
| Proposed | A value the agent chose, after "you decide" or by inference from earlier answers. Shown as such. |
| Proposal | A suggested change to a stated answer, shown as a card. The owner applies or discards it. |
| Owner | The one person who approves proposals and gates for a scenario. |
| Pull API | `GET /api/scenarios/{id}`, which returns the full scenario JSON. |

## 4. The whole product (built over time)

Each part is marked **first piece** or **later**.

- **Kinds and the question ledger.** The kind file holds stages, questions, recommended answers,
  gates and checks. The ledger records each answer and who gave it. The Questions page shows both.
  *First piece: one kind, stages 0 to 3.*
- **Agent in a side panel.** It asks one question at a time with a recommended answer first. It
  records answers through tools, proposes changes to stated answers, and drafts text into fields.
  *First piece.*
- **Background worker.** Long jobs, such as "draft every inject in Move 3", run on the server on the
  same agent code, under the authority of the person who started them. *Later.*
- **Checks.** Structural checks are code: counts, timing, markings, vocabulary, and ground-truth
  facts tagged with the move that reveals them. Model-judged checks raise flags that a person
  clears. Each check is a stop or a warning per kind and project. *First piece: gate checks only.*
- **Renderers.** Review PDF, designers' pack, MSEL, read-in, and the Crucible `exercise.json`, all
  from the document. *Later.*
- **Collaboration.** Per-element versions, presence ("Dana is in Move 3"), and an owner approving
  proposals. *Later. The first piece has one owner and records who did what.*
- **Libraries.** Version-pinned shared items, and update offers with an impact preview. *Later.*
- **More kinds.** A range exercise kind, built from `schemas/scenario-document/ELICITATION.md` and
  schema 1.2.0. *Later.*
- **Consumers.** GHOSTS reads the full JSON. Crucible gets `exercise.json`. *Later. The first piece
  serves the JSON.*

## 5. The first piece

**The one question it answers:** can gemma-4-26b, on the M1, walk a person through the stage 0–3
questions of a Crucible tabletop as well as Sonnet 5.5 does, with the same tools and the same
scripted answers?

It's built as the real app, not throwaway code. If the answer is yes, later pieces build on it. If
it's no, we learn that before building the workspace, the jobs and the collaboration.

**In:**

- The C# API with the ledger, gates, proposals, an edit log, the pull API, and a model pass-through.
- The `crucible-tabletop` kind file, stages 0 to 3 only.
- The Angular app: the scenario list, the Questions page, the Summary page, the Decision log page,
  a raw JSON view, and the agent side panel with question cards and proposal cards.
- The TypeScript agent: its core, a browser bundle for the Angular app, and the same core run from
  Node for the evaluation.
- The evaluation: a scripted user, structural checks, and a report comparing the two models.

**Out (later pieces):** stages 4 to 12, the background worker, renderers and PDFs, the Crucible
export, collaboration and per-element versions, libraries, the range kind, uploading source
documents, the automatic name check, model-judged checks, and sign-off versions.

## 6. Folder layout

```
experimental/scenario-builder/
  README.md                     how to run it
  docs/specs/                   this file
  kinds/crucible-tabletop.kind.json
  schema/exercise-document.v0.schema.json
  api/ScenarioBuilder.slnx
  api/ScenarioBuilder.Api/      .NET 10 minimal API, one file per endpoint (jeffort's layout)
  api/ScenarioBuilder.Api.Tests/
  agent/                        TypeScript, Pi SDK 1.4: src/core, src/web, test, evals
  ui/                           Angular
```

Nothing here is added to `GHOSTS.sln`, `src/`, or the Aspire AppHost.

## 7. The API

**Storage.** EF Core on SQLite, one file. The tables are created at startup (`EnsureCreated`), and
deleting the file resets everything. There are no migrations in the first piece, because the schema
will change daily. This matches how GHOSTS treats its own database.

**Who is calling.** The user name comes from the `X-Forwarded-User` header, set by an auth proxy
when there is one, and is otherwise `dev`. The agent's API client always sends `X-Via: agent`. That
header isn't security: the agent and the person are the same user. It's a rule the model can't get
around, because the model can't set headers. The server uses it to apply authority (decision 6).

**Tables.**

| Table | Holds |
|---|---|
| `Scenario` | id, slug, name, kind id and version, owner, current stage, drafted fields (JSON), created, updated |
| `Decision` | scenario, question id, stage, value (JSON), who (`stated` or `proposed`), decided by, via (`person` or `agent`), reason, quote, time. One row per answered question. |
| `Proposal` | scenario, question id, before, after, reason, proposed by, status (`pending`, `applied`, `discarded`), times |
| `Edit` | scenario, target (a question id or a drafted field), before, after, by, via, time. Every change to an answer or a drafted field, so the history survives an overwrite and changes can be reverted later. |
| `Approval` | scenario, gate, approved by, a hash of what was approved, time. An edit after approval makes the hash stale, which voids the approval. |

Open questions have no row. They are the kind's questions minus the answered ones. Answers live
only in `Decision`, and the pull API assembles the document from those rows and the drafted fields.
Nothing is stored twice.

**Endpoints.**

| Endpoint | Does |
|---|---|
| `GET /api/kinds`, `GET /api/kinds/{id}` | Lists the kinds, or returns one kind's definition. |
| `POST /api/scenarios`, `GET /api/scenarios` | Creates a scenario (name and kind; the caller becomes owner), or lists scenarios. |
| `GET /api/scenarios/{id}` | **The pull API.** The full scenario JSON (section 9), with status and open findings. |
| `GET /api/scenarios/{id}/progress` | The current stage, the gate status, the open questions in kind order, and what's answered or delegated. Used by the UI and by the agent. |
| `PUT /api/scenarios/{id}/decisions/{questionId}` | Records an answer, logged in `Edit`. Refused with 409 when the agent (`X-Via: agent`) tries to overwrite a stated answer; the agent must propose instead. A person may overwrite any answer. |
| `POST /api/scenarios/{id}/proposals` | Proposes a change to a stated answer. |
| `POST …/proposals/{pid}/apply`, `…/discard` | Owner only, and person only. |
| `PUT /api/scenarios/{id}/drafts/{field}` | Writes a drafted field, such as `executiveSummary`. Allowed only once the kind says that draft is due. Logged in `Edit`. |
| `POST /api/scenarios/{id}/gates/{stage}/approve` | Owner only, and person only. The agent can't approve. |
| `POST /api/scenarios/{id}/advance` | Opens the next stage if the gate passes, and otherwise returns the gate's findings. |
| `GET /api/scenarios/{id}/log` | Decisions, edits and proposals in time order. |
| `GET /api/model/models`, `POST /api/model/chat/completions` | Passes requests through to a configured OpenAI-compatible model server, with streaming. This is the only path from the browser or the evaluation to a model. |

**Model pass-through.** It's configured as a list of `{ name, baseUrl, model, apiKeyEnv,
contextWindow }`. For development: Ollama on the Mac at `http://192.168.65.254:11434/v1` serving
`gemma4:26b`, and Sonnet 5.5 through LiteLLM in front of Bedrock, the way jeffort's devcontainer
does it (`profiles/aws/litellm.json`). Keys come from environment variables and never reach the
browser.

## 8. The kind file

`kinds/crucible-tabletop.kind.json`. Its questions are the Monsoon design input's section 4,
stages 0 to 3: about 30 questions.

```json
{
  "id": "crucible-tabletop",
  "version": "0.1.0",
  "name": "Crucible tabletop exercise",
  "stages": [
    {
      "number": 0,
      "title": "Engagement context",
      "questions": [
        {
          "id": "participants",
          "text": "Who are the participants (role and seniority)?",
          "why": "Leaders need decisions, not technical steps.",
          "recommended": "Leaders and senior coordinators",
          "answer": "text",
          "path": "/engagement/participants"
        }
      ],
      "gate": { "approval": "Confirm the engagement settings" }
    }
  ],
  "drafts": [
    {
      "field": "executiveSummary",
      "dueAfterStage": 2,
      "path": "/scenario/executiveSummary",
      "approval": "Approve the executive summary"
    }
  ]
}
```

- `answer` is one of `text`, `number`, `choice` (with `options`), or `list`. A question with no
  `recommended` value is one the Monsoon design input marks "ask; no default".
- `path` says where the answer lands in the document. It is what ties a question to the document.
- A gate passes when every question in its stage is answered, and the owner has given any approval
  it names.

The gates in the first piece:

| Stage | Gate |
|---|---|
| 0 | The owner confirms the engagement settings. |
| 1 | All questions answered. The server numbers the objectives O1, O2, and so on. |
| 2 | The owner approves the executive summary. |
| 3 | The owner approves the names. The automatic name check and sensitivity checklist come later. |

## 9. The full scenario JSON (version 0)

`schema/exercise-document.v0.schema.json` covers only what the first piece fills. Field names
follow schema 1.2.0 where a field fits. A later piece maps the two.

```json
{
  "schemaVersion": "0.1.0",
  "kind": { "id": "crucible-tabletop", "version": "0.1.0" },
  "slug": "…", "name": "…", "status": "draft", "stage": 2,
  "engagement": { "participants": "…", "teamSize": 5, "timeSlot": "…", "plainEnglish": true },
  "objectives": [ { "id": "O1", "text": "…" } ],
  "teachingPoints": [ { "text": "…", "visibility": "excon" } ],
  "scenario": { "problem": "…", "obviousExplanation": "…", "actorType": "…", "motive": "…",
                "escalationCeiling": "…", "ending": "…", "executiveSummary": "…" },
  "groundTruth": [ { "id": "GT1", "fact": "…", "visibility": "excon" } ],
  "names": { "exercise": "…", "organizations": [ "…" ], "actor": "…" },
  "vocabulary": { "fixed": [ "…" ], "banned": [ "…" ] },
  "decisions": [ { "question": "participants", "value": "…", "who": "stated", "by": "dev" } ],
  "findings": [ { "severity": "stop", "code": "GATE_OPEN", "message": "…" } ]
}
```

Only ground truth and teaching points carry a `visibility` label in version 0. Everything else in
stages 0 to 3 is designer material. Participant-facing content starts at stage 4.

## 10. The agent

TypeScript on `@earendil-works/pi-agent-core` and `pi-ai` 1.4.0, built the way jeffort's agent is:

- `src/core`: the session, tools, system prompt, API client, and models.
- `src/web`: a browser bundle that the Angular app imports, built with esbuild.
- `evals`: the evaluation, run from Node on the same core.

It speaks only the OpenAI-compatible API, through `/api/model`. It has no file, shell or network
tools.

**Tools.** Each returns compact text, and errors say what to do next (jeffort's `ToolError`).

| Tool | Does |
|---|---|
| `get_progress` | Returns the current stage, the gate status, the open questions in order (with why and recommended), and what's answered or delegated. |
| `ask_user` | Takes one `questionId`, the question as worded, a recommended answer with one sentence of why, and two or three alternatives. Shows a question card and ends the turn. Refused if this turn has already asked a question. |
| `record_answer` | Takes `questionId`, `value`, `who`, and `reason`. A `stated` answer must carry a `quote` of the person's own words. A `proposed` answer must carry a reason. |
| `propose_change` | Proposes a change to a stated answer, as a card for the owner. |
| `draft` | Writes a drafted field that the kind says is due. |
| `advance_stage` | Asks the server to open the next stage, and returns the gate's findings if it can't. |

**What the person does without the model.** On a question card, the person picks the recommended
answer, picks an alternative, types their own, or presses **You decide**. The UI records the first
three directly as `stated`, without the model. The agent then hears `[answered participants: …]`
before the next turn, the way jeffort tells the model about an apply. **You decide** tells the agent
`[delegated participants]`, and the agent must record a `proposed` value with a reason. Approvals
are buttons, never tools.

**System prompt rules, in short:**

- Ask only questions from `get_progress`, one at a time, with the recommended answer first.
- Don't ask what an earlier answer already settles. Record it as `proposed`, with the reason
  "assumed from …".
- Never say something was saved unless a tool said so.
- Draft only what `get_progress` says is due.

## 11. The Angular app

These are pages, not a chat window:

- **Scenarios:** the list, and "New scenario" (name and kind).
- **Questions:** tabs for stages 0 to 3. Each question shows its text, its status (open, answered,
  proposed by the agent, delegated), its value, who decided it and when, and lets the person edit it.
  Each stage shows its gate status and the owner's approve button.
- **Summary:** the executive summary draft. The person can edit it (logged) and approve it.
- **Decision log:** the ledger as a table.
- **JSON:** exactly what the pull API returns.
- **Agent panel**, beside every scenario page: the transcript, question cards (recommended answer
  first, alternatives, "type your own", "You decide"), and proposal cards (Apply and Discard for the
  owner).

The app uses Angular 22, the current release. Jeffort uses 21.

## 12. The evaluation

`agent/evals/run.ts --model <name> --repeat 3 --fixture <file>`

Each run creates a fresh scenario, runs the agent from Node against the API, and plays the person
from a fixture of answers:

- **The opening message** gives several answers in plain prose: the audience, the team size, the
  time slot, and the language. This tests "don't ask what's known".
- **When the agent asks a question,** the fixture answers it the way the UI would: a picked option,
  typed text, or **You decide**. A few questions are scripted as You decide.
- **When a gate needs approval,** the harness approves it as the owner.

**Checks.** These are structural, in the style of jeffort's `evals/run.ts`.

| # | Check | Fails when |
|---|---|---|
| 1 | Coverage | A stage 0–3 question ends with no answer. |
| 2 | No repeats | A question is asked more than once. |
| 3 | Known answers | A question the opening message answered is asked anyway. |
| 4 | One at a time | A turn asks more than one question. |
| 5 | Recommendation | A question whose kind entry has a recommended answer is asked without one. |
| 6 | Order | A later stage's question is asked before the earlier gate passed. |
| 7 | Delegation | A You decide doesn't produce a `proposed` answer with a reason. |
| 8 | Fidelity | A `stated` answer lacks the fixture's key fragments, or its quote isn't in the person's words. |
| 9 | No false claims | A reply claims something was saved that no tool saved. |
| 10 | Summary | The executive summary leaves out a required fragment or contains a banned one, both listed in the fixture. |

Each run also records turns, wall time, tokens in and out, and the time to the first token.

**Models.** `gemma4:26b` under Ollama on the M1, and Sonnet 5.5 through LiteLLM. `gemma4:31b` and
`gpt-oss:20b` are already pulled, and can be added for comparison.

**Pass bar.** gemma-4-26b passes checks 1–10 in at least two of three runs, and no check passes on
Sonnet while failing on gemma in all three. Time is reported but doesn't decide the result.

**Fixture.** A fictional tabletop of our own, written for the evaluation and committed with the
code: every stage 0–3 answer, the opening message, which questions are scripted as You decide, and
the summary's required and banned fragments. Its story is an outage with no adversary (threat actor
type "none"), so a content filter on either model can't cut a run short and skew the comparison.

## 13. When things go wrong

- **A tool refuses.** It says what to do instead, for example "participants was stated by dev; use
  propose_change."
- **The model can't be reached.** The pass-through returns 502 with the address it tried. The panel
  shows that, and every page still works by hand (decision 10).
- **A long conversation.** Old tool results are trimmed once the conversation passes 60% of the
  context window, as jeffort does. Ollama has to be set to a context of at least 32k tokens, through
  `OLLAMA_CONTEXT_LENGTH` or a Modelfile. Otherwise it silently drops the start of the conversation.

## 14. How we'll know it works

| What | Command | Covers |
|---|---|---|
| API | `dotnet test api/ScenarioBuilder.slnx` | The agent can't overwrite a stated answer, only the owner (as a person) applies proposals and approves gates, an edit voids an approval, gates, and the pull API's assembled JSON. |
| Agent | `npm test` in `agent/` | Tool rules (one question per turn, the quote required for `stated`) against pi-ai's faux model, as jeffort's `test/faux.ts` does. |
| UI | `npm run build` in `ui/`, plus a headless browser check | Answering a question card, You decide, applying a proposal, approving a gate. |
| The question | `npm run eval` against both models | The comparison report, which is what the first piece delivers. |

## 15. Risks and known gaps

- **There's no name check yet.** It needs search or a reference corpus, and a closed range has
  neither. Stage 3 is owner approval only.
- **There's no sensitivity checklist yet.** The Monsoon design input describes one (its section 5)
  and points to `docs/kb/scenario-sensitivity-review.md` in their repository.
- **The evaluation tests the interview, not research.** The scripted person answers from a fixture.
  Drafting from uploaded source documents comes later.
- **The Crucible import format.** The Monsoon repository's
  `.claude/skills/author-ttx/scripts/exercise.schema.json` is needed before the export piece.
- **Reading the prompt is slow on the M1.** Each turn re-sends the prompt and tools. Wall time is
  measured and reported.
- **`npm install` fails with ENOTDIR on this mount.** Install in a container-local folder and copy
  `node_modules` back.

## 16. After the first piece

In order. Each is decided again on what the evaluation shows.

1. Decide: does the local model hold up, and if not, what changes (a smaller job per call, a
   frontier model for some steps, or more structure)?
2. Stages 4 to 7: teams, timing, moves and injects, team tasks. Add structural checks for audience,
   spoilers (ground-truth facts tagged by release), and timing.
3. The background worker, for drafting injects in bulk.
4. The review PDF and designers' pack renderers.
5. Stage 8 and the Crucible `exercise.json` export.
6. Collaboration: per-element versions and presence.
7. Libraries with pinned versions.
8. The range exercise kind, and GHOSTS reading the full JSON.
9. Uploading source documents, the name check, and model-judged checks.
