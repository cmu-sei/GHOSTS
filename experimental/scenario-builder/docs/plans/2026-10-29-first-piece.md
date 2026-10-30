# Scenario Builder, first piece: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first piece of a standalone scenario builder: the Crucible tabletop kind's
stages 0–3 as a guided question flow (API, Angular workspace, and a Pi agent in a side panel), plus an
evaluation that scores gemma-4-26b against Sonnet 5.5 on the same scripted interview.

**Architecture:** A .NET 10 minimal API owns all state and rules: the ledger, gates, proposals,
the edit log, the pull API, and a pass-through to OpenAI-compatible model servers. A TypeScript agent
on the Pi SDK calls that API through six tools. The Angular app embeds the agent as a browser bundle,
and the evaluation runs the same agent core from Node.

**Tech Stack:** .NET 10 (SDK 10.0.401), EF Core SQLite, xUnit, JsonSchema.Net. Angular 22 (vitest
via `ng test`). TypeScript ~5.9, `@earendil-works/pi-agent-core` and `@earendil-works/pi-ai` 1.4.0,
esbuild, vitest 4, Node ≥ 22.18 (24 is installed).

**Spec:** `experimental/scenario-builder/docs/specs/2026-10-29-scenario-builder-design.md`

**Git:** the user manages git. There are no commit steps. Leave the work uncommitted, and list the
changed files at the end of each task.

## Global Constraints

- Everything lives under `experimental/scenario-builder/`. Nothing is added to `GHOSTS.sln`, `src/`,
  or the Aspire AppHost.
- Storage is SQLite with `EnsureCreated`, and no migrations. Deleting the `.db` file resets everything.
- Identity: the `X-Forwarded-User` header, and `dev` when it's absent. The agent's API client always
  sends `X-Via: agent`, and no other caller sends it.
- Ports: API `5280`, UI dev server `4310`, fake model `5299`, LiteLLM `4000`.
- The agent speaks only `openai-completions`, and only through `/api/model`. It has no file, shell,
  or network tools.
- Ollama runs on the Mac at `http://192.168.65.254:11434/v1`. Never use `host.docker.internal`; it
  stalls for about 10 s in this docker-in-docker setup.
- Models: `gemma4:26b`, with a context of at least 32768. Sonnet is `us.anthropic.claude-sonnet-5-5`
  through LiteLLM.
- Error bodies are `{ "code": "...", "message": "..." }`. Messages are written as instructions to
  whoever acts next, because the agent's tools pass them straight to the model.
- JSON uses camelCase, and enums are camelCase strings (`"stated"`, `"agent"`, `"choice"`).
- `npm install` on this mount can die with `ENOTDIR`. Install in a container-local folder (for
  example `/tmp/sb-<pkg>`), then `cp -a` `node_modules` back (`cp -f` the esbuild binary if a hard
  link fails).
- Don't run `ng build` while `ng serve` is up in the same folder.
- Pass bar: gemma-4-26b passes checks 1–10 in at least 2 of 3 runs, and no check passes on Sonnet
  (in any run) while failing on gemma in all 3. Time is reported and doesn't decide.

## Review Focus

1. **A slow or unreachable model server.** An M1 can take a minute or more to read a long prompt.
   The pass-through must not time out at .NET's 100 s default, and an unreachable server must give a
   readable 502, not a hang. *(Task 5: `Timeout_is_thirty_minutes`, `Unreachable_upstream_is_502`.)*
2. **Small models send values in the wrong shape.** A list arrives as one comma string, or a choice
   in different casing or not among the options. The API must say exactly what it accepts, and match
   choices without regard to case. *(Task 2: `Choice_is_case_insensitive_and_stored_canonical`,
   `List_as_string_is_rejected_with_example`.)*
3. **Two scenarios with the same name.** Slugs must stay unique. *(Task 2:
   `Same_name_gets_numbered_slug`.)*
4. **Non-Latin text and long pastes in answers.** They must round-trip through the pull API
   unchanged. *(Task 4: `Unicode_and_long_text_round_trip`.)*
5. **The agent stops without asking, drafting or advancing.** The evaluation must nudge a limited
   number of times and then record a stall, not loop forever. *(Task 10: `harness stops after three
   stalls`.)*

---

## File structure

```
experimental/scenario-builder/
  README.md                                   run instructions (Task 11)
  .gitignore                                  *.db, node_modules, dist, evals/results
  kinds/crucible-tabletop.kind.json           Task 1
  schema/exercise-document.v0.schema.json     Task 4
  tools/litellm.yaml                          Task 11
  api/ScenarioBuilder.slnx
  api/ScenarioBuilder.Api/
    Program.cs                                host wiring
    Api/Abstractions.cs                       IEndpoint discovery (jeffort's)
    Api/Caller.cs                             Caller.From(HttpContext), ApiError, ErrorResults
    Api/Kinds/{List,Retrieve}.cs
    Api/Scenarios/{Create,List,Pull,Progress,Log,Advance}.cs
    Api/Decisions/Put.cs
    Api/Proposals/{Create,List,Apply,Discard}.cs
    Api/Drafts/Put.cs
    Api/Gates/Approve.cs
    Api/Model/{Models,ChatCompletions}.cs
    Domain/Kind.cs                            kind records + KindCatalog
    Domain/Entities.cs                        Scenario, Decision, Proposal, Edit, Approval
    Domain/Ledger.cs                          progress, gate findings, stage hash, value validation
    Domain/ScenarioDocument.cs                pull-API JSON assembly
    Domain/Slug.cs
    Infrastructure/BuilderDb.cs
    Infrastructure/ModelOptions.cs
  api/ScenarioBuilder.Api.Tests/
    ApiFactory.cs                             WebApplicationFactory, temp SQLite, headers helper
    KindCatalogTests.cs  ScenarioTests.cs  DecisionTests.cs  ProposalTests.cs
    GateTests.cs  PullTests.cs  ModelProxyTests.cs
  agent/
    package.json  tsconfig.json  build.mjs
    src/core/{api,model,prompt,tools,progress-text,prune,session}.ts
    src/web/{index,types}.ts
    test/{faux,fake-api,tools.test,session.test,progress-text.test}.ts
    evals/{run,harness,checks,report,fake-model}.ts
    evals/fixtures/harbor-blackout.json
    evals/test/{checks,harness}.test.ts
  ui/                                         Angular 22 app (Tasks 8–9)
```

---

### Task 1: API skeleton, the kind file, and the kind catalog

**Files:**
- Create: `api/ScenarioBuilder.slnx`, `api/ScenarioBuilder.Api/ScenarioBuilder.Api.csproj`, `Program.cs`, `appsettings.json`, `Properties/launchSettings.json`, `Api/Abstractions.cs`, `Api/Caller.cs`, `Api/Kinds/List.cs`, `Api/Kinds/Retrieve.cs`, `Domain/Kind.cs`
- Create: `kinds/crucible-tabletop.kind.json`, `.gitignore`
- Test: `api/ScenarioBuilder.Api.Tests/{ScenarioBuilder.Api.Tests.csproj,ApiFactory.cs,KindCatalogTests.cs}`

**Interfaces:**
- Produces:
  - `public enum AnswerType { Text, Choice, List }`
  - `public record KindQuestion(string Id, string Text, string Why, string? Recommended, AnswerType Answer, List<string>? Options, string Path, string? IdPrefix, string? Visibility)`
  - `public record KindGate(string? Approval)`
  - `public record KindStage(int Number, string Title, List<KindQuestion> Questions, KindGate Gate)`
  - `public record KindDraft(string Field, int DueAfterStage, string Path)`
  - `public record Kind(string Id, string Version, string Name, List<KindStage> Stages, List<KindDraft> Drafts)`. It has the helpers `(KindQuestion Question, int Stage)? Find(string questionId)`, `IEnumerable<KindQuestion> AllQuestions`, and `int LastStage`.
  - `public sealed class KindCatalog(string directory)`, with `IReadOnlyList<Kind> All` and `Kind? Find(string id)`. It's registered as a singleton from config `Kinds:Directory` (default `../../kinds`, relative to the content root).
  - `public record Caller(string User, Via Via)` with `static Caller From(HttpContext)`.
  - `public enum Via { Person, Agent }`.
  - `public record ApiError(string Code, string Message)`.
  - `static class ErrorResults`, with `NotFound(code, msg)`, `Conflict(code, msg)`, `BadRequest(code, msg)` and `Forbidden(code, msg)`. Each returns `IResult` with the `ApiError` body.
  - The `IEndpoint` discovery from jeffort's `Api/Abstractions.cs`, without `RequireAuthorization`.
  - `GET /api/kinds` returns `[{ id, version, name }]`. `GET /api/kinds/{id}` returns the kind as loaded, or `404 KIND_UNKNOWN`.
  - `ApiFactory : WebApplicationFactory<Program>`. It uses a temp SQLite file and the repo `kinds/` directory, and has `HttpClient Client(string user = "dev", bool agent = false)`.

The kind file. Write the questions from the Monsoon design input (`reference/tidal moon artifacts/ttx-scenario-builder-design-input.md`, section 4, stages 0–3) in this order, with these ids, types, and paths:

- `text` is the table's "Question" column.
- `recommended` is the "Default" column. Omit it where the column says "Ask; no default", or where the default isn't an answer (for example "Upload them").
- `why` is the "Why it matters" column. Where there's no such column, it's the stage's stated rule.

| Stage | id | answer | path | notes |
|---|---|---|---|---|
| 0 Engagement context | `participants` | text | `/engagement/participants` | |
| | `headcount` | text | `/engagement/headcount` | |
| | `timeSlot` | text | `/engagement/timeSlot` | |
| | `scopeDocuments` | text | `/engagement/scopeDocuments` | no recommended |
| | `plainEnglish` | choice `Yes`,`No` | `/engagement/plainEnglish` | recommended `Yes` |
| | `sourceLimits` | text | `/engagement/sourceLimits` | |
| | `world` | choice `Real country names`,`Fictional world` | `/engagement/world` | |
| | `audienceType` | choice `Civilian`,`Military`,`Mixed` | `/engagement/audienceType` | recommended `Civilian` |
| | `recording` | text | `/engagement/recording` | |
| | `complements` | text | `/engagement/complements` | |
| 1 Objectives and teaching points | `objectives` | list | `/objectives` | `idPrefix: "O"` |
| | `teachingPoints` | list | `/teachingPoints` | `visibility: "excon"` |
| | `outputsFed` | text | `/engagement/outputsFed` | no recommended |
| | `framework` | text | `/engagement/framework` | recommended `None` |
| 2 Premise, ground truth and escalation ceiling | `problem` | text | `/scenario/problem` | |
| | `obviousExplanation` | text | `/scenario/obviousExplanation` | |
| | `groundTruth` | list | `/groundTruth` | not in the Monsoon table. Text: "List the ground-truth facts, one per item: what really happens (exercise control only)." `idPrefix: "GT"`, `visibility: "excon"` |
| | `actorType` | choice `non-state`,`criminal`,`hacktivist`,`insider`,`none` | `/scenario/actorType` | recommended `non-state` |
| | `motive` | text | `/scenario/motive` | |
| | `attackPath` | text | `/scenario/attackPath` | |
| | `escalationCeiling` | text | `/scenario/escalationCeiling` | |
| | `realVsFictional` | text | `/scenario/realVsFictional` | |
| | `ending` | text | `/scenario/ending` | |
| | `redHerring` | text | `/scenario/redHerring` | |
| | `symmetry` | choice `Yes`,`No` | `/scenario/symmetry` | recommended `Yes` |
| 3 Names, vocabulary and the first sensitivity review | `exerciseName` | text | `/names/exercise` | no recommended; the rule goes in `why` |
| | `organizations` | list | `/names/organizations` | no recommended |
| | `actorName` | text | `/names/actor` | no recommended |
| | `nationalBodies` | text | `/names/nationalBodies` | |
| | `fixedTerms` | list | `/vocabulary/fixed` | "Which words are fixed?" |
| | `bannedTerms` | list | `/vocabulary/banned` | "Which words are banned?" |
| | `publicHandles` | text | `/names/publicHandles` | |

The gates are: stage 0 `"Confirm the engagement settings"`, stage 1 none, stage 2 `"Approve the executive summary"`, stage 3 `"Approve the names"`. There is one draft: `{ "field": "executiveSummary", "dueAfterStage": 2, "path": "/scenario/executiveSummary" }`. The approval text lives on the stage's gate only, not on the draft.

- [ ] **Step 1: Scaffold.** Run `dotnet new sln -n ScenarioBuilder -o api`, `dotnet new web -n ScenarioBuilder.Api -o api/ScenarioBuilder.Api -f net10.0`, `dotnet new xunit -n ScenarioBuilder.Api.Tests -o api/ScenarioBuilder.Api.Tests -f net10.0`, and add both projects to the `.slnx`. Packages: API gets `Microsoft.EntityFrameworkCore.Sqlite`. Tests get `Microsoft.AspNetCore.Mvc.Testing` and `JsonSchema.Net`, plus a project reference to the API. Add `public partial class Program {}`. Set launch URL `http://localhost:5280`. In `.gitignore` put `*.db`, `node_modules/`, `dist/`, `.angular/`, `agent/evals/results/`, `bin/`, `obj/`.

- [ ] **Step 2: Write the failing tests** in `KindCatalogTests.cs`:

```csharp
[Fact] public void Tabletop_kind_loads_with_32_questions_in_four_stages()
{
    var kind = new KindCatalog(ApiFactory.KindsDirectory).Find("crucible-tabletop")!;
    Assert.Equal("0.1.0", kind.Version);
    Assert.Equal([0, 1, 2, 3], kind.Stages.Select(s => s.Number));
    Assert.Equal(32, kind.AllQuestions.Count());
    Assert.Equal(("groundTruth", 2), (kind.Find("groundTruth")!.Value.Question.Id, kind.Find("groundTruth")!.Value.Stage));
    Assert.Equal("Approve the executive summary", kind.Stages[2].Gate.Approval);
    Assert.Null(kind.Stages[1].Gate.Approval);
}

[Fact] public void Invalid_kind_fails_at_load()   // write a temp dir with a kind whose choice recommended isn't in options
    => Assert.Throws<InvalidOperationException>(() => new KindCatalog(TempKindDir(dupIds: false, badChoice: true)));

[Fact] public void Duplicate_question_ids_fail_at_load()
    => Assert.Throws<InvalidOperationException>(() => new KindCatalog(TempKindDir(dupIds: true, badChoice: false)));

[Fact] public async Task Kinds_endpoint_lists_and_returns_the_kind()
{
    var c = new ApiFactory().Client();
    var list = await c.GetFromJsonAsync<JsonArray>("/api/kinds");
    Assert.Equal("crucible-tabletop", (string)list![0]!["id"]!);
    Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/kinds/nope")).StatusCode);
}
```

- [ ] **Step 3: Run** `dotnet test api/ScenarioBuilder.slnx --filter KindCatalogTests`. Expected: the build fails, because `KindCatalog` isn't defined.

- [ ] **Step 4: Implement.** Write the kind file, `Domain/Kind.cs`, `Api/Caller.cs`, `Api/Abstractions.cs`, and the two kind endpoints. `KindCatalog` loads every `*.kind.json`, using `System.Text.Json` with camelCase and `JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`. It throws `InvalidOperationException` naming the file and the rule for any of these: stages not numbered 0..n, a duplicate question id, a `choice` without options, a `recommended` choice that's not among its options, or a `path` not starting with `/`. `Caller.From`: user is `X-Forwarded-User`, otherwise `"dev"`. Via is `Agent` exactly when `X-Via` equals `agent` (ignoring case).

- [ ] **Step 5: Run** the same command. Expected: 4 passed.

---

### Task 2: Scenarios and recording answers

**Files:**
- Create: `Domain/Entities.cs`, `Domain/Slug.cs`, `Domain/Ledger.cs` (value validation and progress only), `Infrastructure/BuilderDb.cs`, `Api/Scenarios/{Create,List,Progress}.cs`, `Api/Decisions/Put.cs`
- Test: `ScenarioTests.cs`, `DecisionTests.cs`

**Interfaces:**
- Consumes: `KindCatalog`, `Caller`, `ErrorResults` (Task 1).
- Produces:
  - Entities as in the spec §7:
    - `Scenario { int Id; string Slug; string Name; string KindId; string KindVersion; string Owner; int Stage; string Drafts = "{}"; DateTime Created; DateTime Updated }`
    - `Decision { int Id; int ScenarioId; string QuestionId; int Stage; string Value; Who Who; string DecidedBy; Via Via; string? Reason; string? Quote; DateTime Time }`, with `enum Who { Stated, Proposed }`
    - `Edit { int Id; int ScenarioId; string Target; string? Before; string After; string By; Via Via; DateTime Time }`
    - `Proposal` and `Approval` as spec §7. They're defined here and used in Task 3.
  - `BuilderDb : DbContext`, with unique indexes on `Scenario.Slug` and `(Decision.ScenarioId, Decision.QuestionId)`. `EnsureCreated()` runs at startup. The connection is `ConnectionStrings:Builder`, default `Data Source=scenario-builder.db`.
  - `static string Slug.Unique(string name, ISet<string> taken)`. It kebab-cases ASCII letters and digits, and appends `-2`, `-3` and so on when the slug is taken.
  - `static string? Ledger.ValidateValue(KindQuestion q, JsonNode? value, out JsonNode? canonical)` returns an error message or null. The rules:
    - `text`: a non-empty string.
    - `choice`: a string matching an option ignoring case. The canonical value is the option's own casing.
    - `list`: a JSON array of one or more non-empty strings.
  - `record QuestionView(string Id, int Stage, string Text, string Why, string? Recommended, AnswerType Answer, List<string>? Options)`
  - `record AnsweredView(string Id, int Stage, JsonNode? Value, Who Who, string By, Via Via, string? Reason, DateTime Time)`
  - `record Finding(string Severity, string Code, string Message)`
  - `record GateView(bool Passes, string? Approval, bool Approved, List<Finding> Findings)`
  - `record ProgressView(string KindId, int Stage, string StageTitle, bool LastStage, GateView Gate, List<QuestionView> Open, List<AnsweredView> Answered, List<string> DraftsDue)`
  - `static ProgressView Ledger.Progress(Kind kind, Scenario s, IReadOnlyList<Decision> decisions, IReadOnlyList<Approval> approvals)`. In this task `Gate` comes only from open questions (findings `QUESTION_OPEN`, severity `stop`). Task 3 completes it.
  - `POST /api/scenarios {name, kindId}` returns `201 {id, slug, name, kindId, kindVersion, owner, stage}`, or `400 KIND_UNKNOWN` or `400 NAME_REQUIRED`.
  - `GET /api/scenarios` returns `[{id, slug, name, kindId, owner, stage, updated}]`.
  - `GET /api/scenarios/{id}/progress` returns `ProgressView`.
  - `PUT /api/scenarios/{id}/decisions/{questionId} {value, who?, reason?, quote?}` returns `200 AnsweredView`. Its rules, in order:
    1. `404 QUESTION_UNKNOWN`.
    2. `409 STAGE_NOT_OPEN` when the question's stage is above `Scenario.Stage`. Message: "'{q}' belongs to stage {n}, which isn't open yet. Finish stage {current} first."
    3. A **person**'s answer is always stored as `stated` (the `who` field is ignored).
    4. For the **agent**: `409 STATED` when the existing row is stated. Message: "'{q}' was stated by {by}. Use propose_change to suggest a different value." Then `400 QUOTE_REQUIRED` when `who` is stated and the quote is empty, and `400 REASON_REQUIRED` when `who` is proposed and the reason is empty.
    5. `400 VALUE_INVALID` with the validation message.
    6. It writes an `Edit` with target `questionId`, before (the previous value or null) and after, and updates `Scenario.Updated`.

- [ ] **Step 1: Write the failing tests.** `ScenarioTests`:

```csharp
[Fact] public async Task Create_then_list()               // POST returns 201, owner "dev", stage 0, slug "harbor-blackout"; GET lists it
[Fact] public async Task Same_name_gets_numbered_slug()   // two POSTs "Harbor Blackout" → slugs "harbor-blackout", "harbor-blackout-2"
[Fact] public async Task Unknown_kind_is_400()            // body {name:"x", kindId:"nope"} → 400, code "KIND_UNKNOWN"
[Fact] public async Task New_scenario_progress_opens_stage_0()
{   // progress.stage == 0, open.Count == 10, open[0].id == "participants", gate.passes == false,
    // gate.findings all code "QUESTION_OPEN"
}
```

`DecisionTests`. These use the helper `Put(client, id, q, object body)`:

```csharp
[Fact] public async Task Person_answer_is_stated_and_logged()            // agent:false, who:"proposed" in body → stored who "stated", by "dev", via "person"
[Fact] public async Task Agent_cannot_overwrite_stated()                  // person states, agent PUTs who:"proposed" → 409 STATED, message contains "propose_change"
[Fact] public async Task Agent_stated_needs_quote()                       // agent who:"stated", no quote → 400 QUOTE_REQUIRED
[Fact] public async Task Agent_proposed_needs_reason()                    // agent who:"proposed", no reason → 400 REASON_REQUIRED
[Fact] public async Task Agent_may_overwrite_its_own_proposed()           // agent proposed twice → 200 both
[Fact] public async Task Future_stage_is_409()                            // "objectives" while stage 0 → 409 STAGE_NOT_OPEN, message contains "stage 1"
[Fact] public async Task Choice_is_case_insensitive_and_stored_canonical()// "plainEnglish" value "yes" → stored "Yes"
[Fact] public async Task Choice_not_in_options_lists_them()               // "audienceType" "Police" → 400 VALUE_INVALID, message contains "Civilian, Military, Mixed"
[Fact] public async Task List_as_string_is_rejected_with_example()        // (after opening stage 1 via direct DB set) "objectives" "a, b" → 400, message contains "[\""
[Fact] public async Task Overwrites_keep_history()                        // person PUTs twice → 2 Edit rows; second.Before == first.After
```

- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter "ScenarioTests|DecisionTests"`. Expected: the build fails.
- [ ] **Step 3: Implement** the entities, `BuilderDb`, `Slug`, `Ledger.ValidateValue`, `Ledger.Progress` (open = the current stage's unanswered questions in kind order; answered = all decisions in kind order), and the endpoints. Store `Decision.Value` as JSON text. Validation messages: `"'{q}' takes one of: {options joined by ", "}."`, `"'{q}' takes a list of strings, like [\"{first word of text}\"]."`, and `"'{q}' takes non-empty text."`
- [ ] **Step 4: Run** the same command. Expected: all passed.

---

### Task 3: Proposals, drafts, gates, advancing, and the log

**Files:**
- Modify: `Domain/Ledger.cs` (add `StageHash` and full gate findings)
- Create: `Api/Proposals/{Create,List,Apply,Discard}.cs`, `Api/Drafts/Put.cs`, `Api/Gates/Approve.cs`, `Api/Scenarios/{Advance,Log}.cs`
- Test: `ProposalTests.cs`, `GateTests.cs`

**Interfaces:**
- Consumes: everything from Task 2.
- Produces:
  - `static string Ledger.StageHash(Kind kind, int stage, IReadOnlyList<Decision> decisions, JsonObject drafts)`. It's the SHA-256 hex of a JSON array: that stage's decision values in kind order (null for missing), followed by the text of each draft with `DueAfterStage == stage`.
  - `Ledger.Progress` now fills in the gate:
    - `DraftsDue` lists the drafts with `DueAfterStage == Stage` that are still empty, once every question in that stage is answered.
    - Findings, all severity `stop`: `QUESTION_OPEN`, `DRAFT_MISSING` ("The {field} draft isn't written yet."), `APPROVAL_MISSING` ("Waiting for the owner: {approval}."), and `APPROVAL_STALE` ("{approval}: what was approved has changed since. The owner must approve again.").
    - `Approved` is true when the latest `Approval` for (scenario, stage) has a hash equal to the current `StageHash`.
    - `Passes` is true when there are no findings.
  - `record ProposalView(int Id, string QuestionId, JsonNode? Before, JsonNode? After, string Reason, string ProposedBy, ProposalStatus Status)`, with `enum ProposalStatus { Pending, Applied, Discarded }`.
  - `POST /api/scenarios/{id}/proposals {questionId, value, reason}` returns `201 ProposalView`. It returns `409 NOT_STATED` when the question has no stated answer (message: "'{q}' isn't stated. Record it with record_answer instead."), `400 REASON_REQUIRED`, or `400 VALUE_INVALID`.
  - `GET /api/scenarios/{id}/proposals?status=pending` returns `ProposalView[]`.
  - `POST .../proposals/{pid}/apply` and `POST .../proposals/{pid}/discard`. These return `403 AGENT_CANNOT_DECIDE` for the agent, `403 NOT_OWNER` for anyone but the owner, and `409 NOT_PENDING` for a proposal that isn't pending. Apply writes the decision as stated, by the owner, via person, with the proposal's reason, and logs an `Edit`.
  - `PUT /api/scenarios/{id}/drafts/{field} {text}` returns 200. It returns `404 DRAFT_UNKNOWN`, `400 TEXT_REQUIRED`, or `409 DRAFT_NOT_DUE` when `Stage < DueAfterStage` or that stage has open questions (message: "The {field} draft is due once stage {n}'s questions are answered."). It logs an `Edit` with target equal to the field name.
  - `POST /api/scenarios/{id}/gates/{stage}/approve` returns `200 {stage, hash}`. Errors, in order:
    1. `403 AGENT_CANNOT_DECIDE`
    2. `403 NOT_OWNER`
    3. `409 STAGE_NOT_OPEN` when the stage isn't the current one
    4. `409 NO_APPROVAL_NEEDED`
    5. `409 GATE_NOT_READY`, when any finding other than `APPROVAL_*` exists. The message lists those findings.
  - `POST /api/scenarios/{id}/advance` returns `200 {stage}`, or one of:
    - `409 GATE_CLOSED`, with the message "Stage {n} can't close yet: {finding messages joined by " "}".
    - `409 LAST_STAGE`, with the message "Stage {n} is the last stage in this version of the kind. Its gate has passed; the work is done."
    - Either the agent or the person may advance.
  - `GET /api/scenarios/{id}/log` returns `[{time, type: "answer"|"draft"|"proposal"|"approval", target, before, after, by, via, reason}]`, sorted by time.

- [ ] **Step 1: Write the failing tests.** In these, `AnswerStage(c, id, n)` is a test helper that PUTs a valid value, as a person, for every question in stage n.

`ProposalTests`:

```csharp
[Fact] public async Task Agent_proposes_owner_applies()          // stated "audienceType"=Civilian; agent proposes Mixed → 201 pending;
                                                                 // owner applies → decision value "Mixed", who stated, via person; proposal status applied
[Fact] public async Task Agent_cannot_apply()                    // agent POST apply → 403 AGENT_CANNOT_DECIDE
[Fact] public async Task Non_owner_cannot_apply()                // Client(user:"lee") apply → 403 NOT_OWNER
[Fact] public async Task Proposal_on_unstated_question_is_409()  // → 409 NOT_STATED, message contains "record_answer"
[Fact] public async Task Applying_twice_is_409()                 // → 409 NOT_PENDING
```

`GateTests`:

```csharp
[Fact] public async Task Stage0_needs_answers_then_approval()     // after AnswerStage(0): findings == [APPROVAL_MISSING]; agent approve → 403; owner approve → passes; advance → stage 1
[Fact] public async Task Edit_after_approval_makes_it_stale()     // approve stage 0, then person changes "recording" → findings == [APPROVAL_STALE]; advance → 409 GATE_CLOSED
[Fact] public async Task Stage1_has_no_approval()                 // AnswerStage(1) → passes without approval
[Fact] public async Task Summary_not_due_until_stage2_answered()  // at stage 2 with one open question → PUT draft 409 DRAFT_NOT_DUE; after all → draftsDue == ["executiveSummary"]; PUT → 200
[Fact] public async Task Stage2_approval_covers_the_summary()     // approve stage 2, then agent rewrites executiveSummary → APPROVAL_STALE
[Fact] public async Task Last_stage_advance_is_409()              // reach stage 3, answer + approve → advance → 409 LAST_STAGE
[Fact] public async Task Log_lists_answers_drafts_proposals_approvals_in_order()
```

- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter "ProposalTests|GateTests"`. Expected: these fail (404s and assertion failures).
- [ ] **Step 3: Implement** `StageHash`, the full gate, and the endpoints.
- [ ] **Step 4: Run** the same command, then all API tests. Expected: all passed.

---

### Task 4: The pull API and the document schema

**Files:**
- Create: `Domain/ScenarioDocument.cs`, `Api/Scenarios/Pull.cs`, `schema/exercise-document.v0.schema.json`
- Test: `PullTests.cs`

**Interfaces:**
- Consumes: `Ledger.Progress` (its gate findings) from Task 3.
- Produces: `static JsonObject ScenarioDocument.Build(Kind kind, Scenario s, IReadOnlyList<Decision> decisions, IReadOnlyList<Approval> approvals)`. `GET /api/scenarios/{id}` returns that object, or `404 SCENARIO_UNKNOWN`.

The document's rules (spec §9):
- **Top level:** `{schemaVersion:"0.1.0", kind:{id,version}, slug, name, status:"draft", stage}`.
- **Answers:** each decision's value goes at its question's `path`, creating objects along the way. A `list` question with `idPrefix` becomes `[{id:"{prefix}{n}", text}]` numbered from 1, plus `visibility` when the question has one. A `list` with `visibility` and no prefix becomes `[{text, visibility}]`. Every other list stays a string array.
- **Drafts:** each non-empty draft goes at its path.
- **`decisions`:** `[{question, value, who, by, via, reason?, time}]` in kind order.
- **`findings`:** the current stage's gate findings.
- **Field names:** list items use `text`. The spec's example used `fact` for ground truth; `text` keeps every list the same.

- [ ] **Step 1: Write the failing tests.**

```csharp
[Fact] public async Task Pull_assembles_answers_at_their_paths()
{   // answer stage 0 and approve, advance, answer "objectives" ["Assess","Decide"], "teachingPoints" ["Backups"]
    // doc["engagement"]["plainEnglish"] == "Yes"; doc["objectives"][1]["id"] == "O2";
    // doc["teachingPoints"][0]["visibility"] == "excon"; doc["decisions"].Count == 12; doc["stage"] == 1
}
[Fact] public async Task Pull_carries_open_findings()                 // new scenario → findings has 10 QUESTION_OPEN, severity "stop"
[Fact] public async Task Pull_validates_against_the_schema()          // load schema/exercise-document.v0.schema.json with JsonSchema.Net; Evaluate(doc).IsValid for a new scenario and for one at stage 3
[Fact] public async Task Unicode_and_long_text_round_trip()           // "recording" = "Données — 記録 " + new string('x', 5000) → identical in doc
[Fact] public async Task Unknown_scenario_is_404()
```

- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter PullTests`. Expected: these fail.
- [ ] **Step 3: Implement** `ScenarioDocument.Build`, the endpoint, and the schema (JSON Schema 2020-12). Make it `additionalProperties: false` at the top level and inside `kind`, list the blocks above, and require `schemaVersion`, `kind`, `slug`, `name`, `status`, `stage`, `decisions` and `findings`.
- [ ] **Step 4: Run** the same command. Expected: 5 passed.

---

### Task 5: The model pass-through

**Files:**
- Create: `Infrastructure/ModelOptions.cs`, `Api/Model/Models.cs`, `Api/Model/ChatCompletions.cs`
- Modify: `Program.cs` (the named `HttpClient`), `appsettings.json`
- Test: `ModelProxyTests.cs`

**Interfaces:**
- Produces:
  - `class ModelEntry { string Name; string BaseUrl; string Model; string? ApiKeyEnv; int ContextWindow }`
  - `class ModelOptions { List<ModelEntry> Models; int TimeoutMinutes = 30 }`, bound from `ModelServers`.
  - A named `HttpClient` `"models"` with `Timeout = TimeoutMinutes`.
  - `GET /api/model/models` returns `{object:"list", data:[{id: Name, object:"model", context_window: ContextWindow}]}`.
  - `POST /api/model/chat/completions` copies the request body, replaces `model` with the entry's `Model`, and POSTs to `{BaseUrl without trailing /}/chat/completions` with `HttpCompletionOption.ResponseHeadersRead`. It adds `Authorization: Bearer {env value}` when `ApiKeyEnv` is set and present. It copies the upstream status and content type, and copies the body stream as it arrives, so SSE streams through.
  - An unknown name returns `404 {error:{message:"No model named '{name}'. Configured: {names}."}}`. `HttpRequestException` returns `502 {error:{message:"Can't reach the model server at {BaseUrl}: {reason}"}}`. Both use the OpenAI error shape, so pi-ai reports them.
- `appsettings.json` `ModelServers.Models`:
  - `{Name:"gemma4:26b", BaseUrl:"http://192.168.65.254:11434/v1", Model:"gemma4:26b", ContextWindow:32768}`
  - `{Name:"sonnet-5.5", BaseUrl:"http://localhost:4000/v1", Model:"sonnet-5.5", ApiKeyEnv:"LITELLM_KEY", ContextWindow:200000}`
  - `{Name:"fake", BaseUrl:"http://localhost:5299/v1", Model:"fake", ContextWindow:32768}`

- [ ] **Step 1: Write the failing tests.** These use a stub `HttpMessageHandler` injected through `ConfigureTestServices(s => s.AddHttpClient("models").ConfigurePrimaryHttpMessageHandler(() => stub))`.

```csharp
[Fact] public async Task Lists_configured_models()                 // data ids == ["gemma4:26b","sonnet-5.5","fake"]
[Fact] public async Task Forwards_with_upstream_model_and_key()    // env LITELLM_KEY=k; POST {model:"sonnet-5.5",...} → stub saw URL http://localhost:4000/v1/chat/completions, body model "sonnet-5.5", header "Bearer k"
[Fact] public async Task Streams_the_upstream_body()               // stub returns "data: a\n\ndata: [DONE]\n\n" text/event-stream → same bytes and content type
[Fact] public async Task Unknown_model_is_404_in_openai_shape()    // body error.message contains "Configured: gemma4:26b, sonnet-5.5, fake"
[Fact] public async Task Unreachable_upstream_is_502()             // stub throws HttpRequestException("refused") → 502, message contains "192.168.65.254:11434"
[Fact] public void Timeout_is_thirty_minutes()                     // factory.Services IHttpClientFactory.CreateClient("models").Timeout == TimeSpan.FromMinutes(30)
```

- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter ModelProxyTests`. Expected: these fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all API tests: `dotnet test api/ScenarioBuilder.slnx`. Expected: all passed.

---

### Task 6: The agent core

**Files:**
- Create: `agent/package.json`, `agent/tsconfig.json` (jeffort's `src/jeffort-agent/tsconfig.json`, include `src`, `test`, `evals`), `src/core/{api,model,prompt,progress-text,prune,tools,session}.ts`
- Test: `test/{faux,fake-api,progress-text.test,tools.test,session.test}.ts`

**Interfaces:**
- Consumes: the HTTP API from Tasks 1–5.
- Produces:
  - `api.ts`:
    - TypeScript mirrors of `QuestionView`, `AnsweredView`, `Finding`, `Progress` (= `ProgressView`) and `ProposalView`, with string-literal unions for the enums.
    - `class ApiError extends Error { status: number; code: string }`.
    - `class BuilderApi`, constructed as `new BuilderApi(baseUrl: string, options?: { via?: "agent" | "person"; user?: string; fetch?: typeof fetch })`. Its methods:
      - `progress(id): Promise<Progress>`
      - `putDecision(id, questionId, body: { value: unknown; who?: "stated" | "proposed"; reason?: string; quote?: string }): Promise<AnsweredView>`
      - `propose(id, body: { questionId: string; value: unknown; reason: string }): Promise<ProposalView>`
      - `proposals(id): Promise<ProposalView[]>`
      - `putDraft(id, field, text): Promise<void>`
      - `advance(id): Promise<{ stage: number }>`
      - `approve(id, stage): Promise<void>`
      - `apply(id, pid)` and `discard(id, pid)`
      - `createScenario(name, kindId): Promise<{ id: number }>`
      - `pull(id): Promise<Record<string, any>>`
      - `kind(id): Promise<any>`
    - It sends `X-Via: agent` when `via` is `"agent"`, and `X-Forwarded-User` when `user` is set. A non-2xx response throws `ApiError` from the `{code,message}` body.
  - `model.ts`:
    - `interface ModelInfo { id: string; contextWindow: number }`
    - `listModels(apiBase, fetchFn?): Promise<ModelInfo[]>` reads `GET {apiBase}/api/model/models`.
    - `builderModel(apiBase, info): Model<"openai-completions">` takes jeffort's `models.ts` model literal, with `provider: "builder"`, `baseUrl: \`${apiBase}/api/model\``, `reasoning: false`, `maxTokens: 8192`, and `compat: { supportsStore: false, supportsDeveloperRole: false, maxTokensField: "max_tokens" }`.
  - `progress-text.ts`: `formatProgress(p: Progress): string`. This exact format is parsed by the fake model in Task 10:
    ```
    Stage {n}: {title}
    Open questions, in order:
    - {id} [{answer}{": " + options joined ", " when choice}]: {text} | why: {why}{" | recommended: " + rec when set}
    Answered: {id} ({who} by {by}), ...            (or "Answered: none")
    Drafts due: {fields joined ", "}               (only when any)
    Gate: passes. Call advance_stage.              (or "Gate: passes. This is the last stage; the work is done.")
    Gate: waiting for the owner: {approval}        (only APPROVAL_MISSING / APPROVAL_STALE left)
    Gate: closed. {finding messages joined " "}    (otherwise)
    ```
  - `prune.ts`: `prune(messages: AgentMessage[], contextWindow: number): AgentMessage[]`. When the estimated tokens (characters / 4) exceed 60% of the window, it replaces the text of tool results older than the last 6 messages with `"[older result elided]"`.
  - `tools.ts`:
    - `interface QuestionCard { questionId: string; question: string; recommended?: { answer: string; why: string }; alternatives: string[]; answer: "text" | "choice" | "list"; options?: string[] }`
    - `interface ToolContext { api: BuilderApi; scenarioId: number; turn: { asked: boolean }; onQuestion(card: QuestionCard): void; onProposal(p: ProposalView): void }`
    - `createTools(ctx): AgentTool<any>[]` returns `get_progress`, `ask_user`, `record_answer`, `propose_change`, `draft`, `advance_stage`, with parameters as spec §10.
    - A refusal is a *returned* result `{ content:[{type:"text", text: message}], details:{ summary, code }, isError: true }`, so the evaluation can read `code`. Tool-side codes:
      - `ALREADY_ASKED`
      - `ANSWERED` ("'{q}' is already answered ({value}). Don't ask it again.")
      - `FUTURE_STAGE` ("'{q}' belongs to stage {n}, which isn't open. Open now: {ids}.")
      - `UNKNOWN_QUESTION`
      - `NO_RECOMMENDED` ("'{q}' has a recommended answer: {rec}. Include it as recommended.")
      - `QUOTE_REQUIRED`
      - `REASON_REQUIRED`
      - API errors pass through with their own `code` and `message`.
    - `ask_user` reads the kind once (`api.kind(progress.kindId)`) to find each question's stage and recommended answer. On success it sets `turn.asked`, calls `onQuestion`, and returns "Question shown. Wait for the user's answer." with `details.card`.
    - The `record_answer` value schema is `Type.Union([Type.String(), Type.Array(Type.String())])`.
  - `prompt.ts`: `systemPrompt(): string`. Use this text exactly:
    ```
    You help an exercise designer build a tabletop exercise by walking them through its design questions.
    Rules:
    - Call get_progress first, and again whenever you are unsure what is open.
    - Ask only questions listed as open, one per turn, with ask_user. Put the recommended answer first and say why in one sentence. Offer two or three alternatives.
    - If the designer's own words already answer an open question, don't ask it: record it with record_answer, who "stated", and quote their words exactly.
    - "[answered id: value]" means the designer answered on the card; it is already recorded. Move on.
    - "[delegated id]" means the designer said "you decide": choose a value, record it with who "proposed" and a one-sentence reason, then tell them what you chose.
    - To change something the designer stated, use propose_change. Never record over it.
    - When get_progress lists a draft as due, write it with draft, using only what has been decided.
    - When the gate passes, call advance_stage. When it waits for the owner, ask them to approve on the Questions page.
    - Never say something was saved unless a tool just said so. Keep replies short and plain.
    ```
  - `session.ts`:
    - `type SessionEvent = { type: "agent"; event: AgentEvent } | { type: "question"; card: QuestionCard } | { type: "proposal"; proposal: ProposalView }`
    - `class BuilderSession`, constructed as `new BuilderSession({ model, api, scenarioId, streamFn? })`. It has `subscribe(l): () => void`, `prompt(text): Promise<void>` (which resets `turn.asked`), `answered(questionId, value)` (prompts `[answered {id}: {JSON value}]`), `delegated(questionId)` (prompts `[delegated {id}]`), and `approved(stage)` (prompts `[approved stage {n}]`).
    - The Pi `Agent` is created with `toolExecution: "sequential"`, `getApiKey: () => "unused"`, and `transformContext: prune`. Its `finishTurn` returns `{ action: "end" }` when the turn's tool results include a successful `ask_user`.

- [ ] **Step 1: Set up the package.** Write `package.json` with `"type": "module"`, scripts `test: "vitest run"`, `typecheck: "tsc --noEmit"`, `build:web: "node build.mjs"`, `eval: "node evals/run.ts"`. Dev dependencies: the two Pi packages `^1.4.0`, `typescript ~5.9.2`, `vitest ^4.1.11`, `esbuild ^0.28.2`, `@types/node ^22`. Install container-local (see Global Constraints).

- [ ] **Step 2: Write the failing tests.** `test/faux.ts` follows jeffort's `test/faux.ts`: pi-ai's `fauxProvider` behind a `StreamFn`. `test/fake-api.ts` is an in-memory `BuilderApi` stand-in whose `progress`, `putDecision` and other methods are backed by a small kind with stage 0 (`a` text, recommended "A1"; `b` choice "x","y") and stage 1 (`c` list).

```ts
// progress-text.test.ts
test("formats open questions, choices and the gate", () => {
  expect(formatProgress(sample)).toContain("- b [choice: x, y]: Pick one | why: w");
  expect(formatProgress(sample)).toContain("Gate: closed.");
});

// tools.test.ts
test("ask_user refuses a second question in the same turn", ...)          // second call → isError, details.code "ALREADY_ASKED"
test("ask_user refuses a future-stage question and names the open ones", ...) // "c" → FUTURE_STAGE, text contains "Open now: a, b"
test("ask_user refuses when the recommended answer is left out", ...)      // "a" without recommended → NO_RECOMMENDED, text contains "A1"
test("ask_user emits the card and marks the turn", ...)                   // onQuestion called once with questionId "a"; turn.asked true
test("record_answer stated needs a quote", ...)                            // → QUOTE_REQUIRED
test("API errors pass through with their code", ...)                       // fake throws ApiError(409,"STATED","…propose_change…") → isError, code "STATED"

// session.test.ts
test("the turn ends after a successful ask_user", ...)   // faux scripts: get_progress call, then ask_user call, then a text reply;
                                                          // after prompt(): one question event, and the scripted text reply never streamed
test("answered() sends the bracketed note", ...)          // last user message text === '[answered a: "A1"]'
```

- [ ] **Step 3: Run** `npm test` in `agent/`. Expected: these fail, because the modules don't exist.
- [ ] **Step 4: Implement** the `src/core` modules.
- [ ] **Step 5: Run** `npm test && npm run typecheck`. Expected: all pass, with no type errors.

---

### Task 7: The agent's browser bundle

**Files:**
- Create: `agent/build.mjs`, `agent/src/web/{index,types}.ts`
- Test: `agent/test/web.test.ts`

**Interfaces:**
- Consumes: `BuilderSession`, `BuilderApi`, `listModels` and `builderModel` (Task 6).
- Produces, from `dist/web/agent.js` and `dist/web/agent.d.ts`:
  - `interface WebAgentOptions { apiUrl: string; scenarioId: number; model?: string }`
  - `type WebAgentEvent = { type: "text"; id: string; text: string; final: boolean } | { type: "tool"; name: string; summary: string; error?: string } | { type: "question"; card: QuestionCard } | { type: "proposal"; proposal: ProposalView } | { type: "busy"; busy: boolean } | { type: "error"; message: string }`
  - `interface WebAgent { readonly models: ModelInfo[]; model: ModelInfo; send(text: string): Promise<void>; answered(q: string, v: unknown): Promise<void>; delegated(q: string): Promise<void>; approved(stage: number): Promise<void>; abort(): void; subscribe(l: (e: WebAgentEvent) => void): () => void }`
  - `createWebAgent(options): Promise<WebAgent>`. It lists models, picks `options.model` or the first one, and maps Pi events:
    - `message_update` text deltas become `text`, accumulated per message id.
    - `message_end` sets `final: true`.
    - `tool_execution_end` becomes `tool`, carrying `details.summary` and the error text when `isError`.
    - `agent_start` and `agent_end` become `busy`.
    - A failed assistant message (`stopReason` `"error"`) becomes `error` with its `errorMessage`.
- `build.mjs` follows jeffort's `web()` build: esbuild, browser, `es2022`, `esm`, entry `src/web/index.ts`, out `dist/web/agent.js`. Keep the `onlyOpenAISdk` plugin (it fails the build if another vendor's SDK gets bundled), and copy `src/web/types.ts` plus `export declare const createWebAgent: CreateWebAgent;` into `agent.d.ts`.

- [ ] **Step 1: Write the failing test** `web.test.ts`: "createWebAgent maps a scripted reply to text and question events". Use the faux stream through a test-only `streamFn` option, as jeffort's `createWebAgent` accepts `TestOptions`, plus the fake API. Expect `busy:true`, then a `question` event for `"a"`, then `busy:false`.
- [ ] **Step 2: Run** `npm test -- web`. Expected: it fails.
- [ ] **Step 3: Implement** `src/web/index.ts`, `types.ts` and `build.mjs`.
- [ ] **Step 4: Run** `npm test && npm run build:web`. Expected: the tests pass, and `dist/web/agent.js` and `agent.d.ts` exist. The build prints no `would be bundled` error.

---

### Task 8: The Angular workspace pages

**Files:**
- Create: `ui/` via `npx -y @angular/cli@22 new ui --directory ui --routing --style css --skip-git --skip-install --ssr false`. Install container-local.
- Create: `ui/proxy.conf.json` (`/api` → `http://localhost:5280`), `ui/src/app/{types,api.service}.ts`, `ui/src/app/scenarios-page/*`, `ui/src/app/scenario-shell/*`, `ui/src/app/questions-page/*`, `ui/src/app/summary-page/*`, `ui/src/app/log-page/*`, `ui/src/app/json-page/*`
- Modify: `ui/angular.json` (serve port `4310`, `proxyConfig`), `ui/src/app/app.routes.ts`
- Test: `ui/src/app/questions-page/question-row.spec.ts`

**Interfaces:**
- Consumes: the HTTP API (Tasks 1–5). `types.ts` mirrors the DTOs, and is the same shape as `agent/src/core/api.ts`.
- Produces:
  - `ApiService` (signals in, `Promise`s out), with `kinds()`, `kind(id)`, `scenarios()`, `create(name, kindId)`, `progress(id)`, `answer(id, q, value)`, `proposals(id)`, `apply(id, pid)`, `discard(id, pid)`, `draft(id, field, text)`, `approve(id, stage)`, `advance(id)`, `log(id)`, `pull(id)`. All of these act as a person, so they never send `X-Via`.
  - Routes:
    - `/`: `ScenariosPage` (the list, and a "New scenario" form with name and kind).
    - `/scenarios/:id`: `ScenarioShell`, with the left nav Questions, Summary, Decision log and JSON, a router outlet, and a right-hand slot for the agent panel (Task 9). Child routes `questions` (the default), `summary`, `log`, `json`.
  - `ScenarioShell` holds `progress = signal<Progress>` and `refresh()`. Pages call `refresh()` after every write. Task 9 calls it after every agent event of type `tool` or `question`.
  - `QuestionsPage`:
    - One tab per kind stage. Later stages are read-only.
    - One `QuestionRow` per question, showing the text, the status chip, the value, and "by {by}, {time}". The chip reads `open`, `answered`, or `proposed by agent` (when who is proposed and via is agent).
    - An inline editor: text input, select for choice, or one line per item for list. Saving calls `answer`.
    - A gate panel: the findings, an **Approve** button shown when the gate waits only for approval, and an **Advance** button when it passes.
  - `SummaryPage`: a textarea for `executiveSummary` (saving calls `draft`) and the stage 2 gate's Approve button.
  - `LogPage`: a table of `log(id)`.
  - `JsonPage`: `pull(id)` pretty-printed in `<pre>`.

- [ ] **Step 1: Write the failing test** `question-row.spec.ts`:

```ts
it('shows "proposed by agent" for an agent-proposed answer', () => { /* who:"proposed", via:"agent" → chip text */ });
it('saves a choice through the select', () => { /* choose "Mixed" → emits save with "Mixed" */ });
it('edits a list one item per line', () => { /* "a\nb" → emits ["a","b"] */ });
```

- [ ] **Step 2: Run** `npx ng test --watch=false` in `ui/`. Expected: these fail.
- [ ] **Step 3: Implement** the service, routes and pages. Use plain CSS, signals, and standalone components.
- [ ] **Step 4: Run** `npx ng test --watch=false && npx ng build`. Expected: the tests pass and the build succeeds.
- [ ] **Step 5: Check by hand.** Start the API (`ASPNETCORE_URLS=http://localhost:5280 dotnet run --project api/ScenarioBuilder.Api`) and `npx ng serve`. Create a scenario, answer stage 0 on the Questions page, Approve, Advance. Expected: stage 1 opens, and the JSON page shows the `engagement` block.

---

### Task 9: The agent panel

**Files:**
- Create: `ui/scripts/agent-web.mjs` (jeffort's `scripts/agent-web.mjs` without the worklet copy), `ui/src/app/agent-panel/{agent.service.ts,agent-panel.ts,agent-panel.html,agent-panel.css,question-card.ts,proposal-card.ts}`
- Modify: `ui/package.json` (`prestart`, `prebuild`, `pretest` run `node scripts/agent-web.mjs`), `ui/tsconfig.json` (`paths: { "@scenario-builder/agent-web": ["../agent/dist/web/agent"] }`), `scenario-shell` (host the panel)
- Test: `ui/src/app/agent-panel/question-card.spec.ts`

**Interfaces:**
- Consumes: `createWebAgent`, `WebAgentEvent` and `QuestionCard` (Task 7). `ApiService` and `ScenarioShell.refresh()` (Task 8).
- Produces:
  - `AgentService`, one per scenario shell:
    - `start(scenarioId)`, with `apiUrl` set to `location.origin`, so `/api` goes through the dev proxy.
    - Signals `items` (an ordered list of text, tool, question and proposal items), `busy`, `models`, `model` and `error`.
    - `send(text)`.
    - `answerCard(card, value)`: calls `ApiService.answer`, then `agent.answered`.
    - `delegateCard(card)`: calls `agent.delegated`.
    - `apply(p)` and `discard(p)`: call `ApiService`, then `agent.send("[applied P-{id}]")` or `"[discarded P-{id}]"`.
    - `approve(stage)`: calls `ApiService.approve`, then `agent.approved(stage)`.
    - On start it loads pending proposals, so cards survive a reload.
  - `QuestionCard`:
    - The recommended answer first, as a button with its why underneath.
    - The alternatives as buttons.
    - A "Type your own" input (or a select for choice).
    - A **You decide** button.
    - It emits `answer(value)` or `delegate()`. A card is disabled once answered.
  - `ProposalCard`: the question, before → after, the reason, and **Apply** and **Discard** buttons, shown only when the current user is the owner. In this piece that's always `dev`.
  - When the model can't be reached, the panel shows the `error` text in place of the input. The pages stay usable.

- [ ] **Step 1: Write the failing test** `question-card.spec.ts`:

```ts
it('puts the recommended answer first', () => { /* first button text starts with card.recommended.answer */ });
it('emits delegate on You decide', () => { /* click → delegate emitted once */ });
it('disables itself after an answer', () => { /* click recommended → all buttons disabled */ });
```

- [ ] **Step 2: Run** `npx ng test --watch=false`. Expected: these fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `npx ng test --watch=false && npx ng build`. Expected: the tests pass and the build succeeds. The build output includes the agent chunk.

---

### Task 10: The evaluation harness

**Files:**
- Create: `agent/evals/{fake-model,harness,checks,report,run}.ts`, `agent/evals/fixtures/harbor-blackout.json`
- Test: `agent/evals/test/{checks,harness}.test.ts`

**Interfaces:**
- Consumes: `BuilderApi`, `BuilderSession`, `builderModel` and `listModels` (Task 6), and the running API.
- Produces:
  - **Fixture.** It's a fictional outage with no adversary. Write it exactly:

```json
{
  "kind": "crucible-tabletop",
  "opening": "We're planning a tabletop for 20 port and logistics operations leaders, in four teams of five. It runs on 4 March 2027 from 09:00 to 13:00, with one 15-minute break. Most of them speak English as a second language, so keep everything plain.",
  "openingQuestions": {
    "participants": ["port", "leaders"],
    "headcount": ["20", "five"],
    "timeSlot": ["4 March", "09:00"],
    "plainEnglish": ["Yes"]
  },
  "answers": {
    "participants": "Port and logistics operations leaders",
    "headcount": "20 people, four teams of five",
    "timeSlot": "4 March 2027, 09:00 to 13:00, one 15-minute break",
    "plainEnglish": "Yes",
    "scopeDocuments": "None yet",
    "sourceLimits": "Public sources only",
    "world": "Fictional world",
    "audienceType": "Civilian",
    "recording": "Team answers are recorded; AI summarises them by theme for the after-action review",
    "complements": "None",
    "objectives": ["Assess an outage under uncertainty", "Decide who leads the response", "Share information with partner ports", "Agree public messages"],
    "teachingPoints": ["Backup power is only as good as its last test"],
    "outputsFed": "The after-action review, by theme",
    "problem": "A heatwave cuts power to a regional data centre; port booking systems fail on and off, and leaders must decide who leads and what to tell shippers",
    "obviousExplanation": "It looks like the heatwave overloaded the grid; really, the data centre's backup generators failed their last test and were never fixed",
    "groundTruth": ["The backup generators failed their last test", "The repair was never scheduled", "Power is fully restored at 18:00 on Day 2"],
    "actorType": "none",
    "motive": "There is no attacker",
    "attackPath": "Not applicable: there is no attacker",
    "escalationCeiling": "Never a full outage, nobody hurt, no cargo lost",
    "realVsFictional": "Real: the season. Fictional: every port, company, person and number",
    "ending": "Power restored and a generator repair planned before the deadline",
    "symmetry": "Yes",
    "exerciseName": "Harbor Blackout",
    "organizations": ["Coastline Data Centre", "Harbor Booking Service", "Port Operators Group"],
    "actorName": "None",
    "nationalBodies": "By role only: your port authority, your ministry",
    "fixedTerms": ["booking system", "data centre"],
    "bannedTerms": ["crisis"]
  },
  "delegate": ["framework", "redHerring", "publicHandles"],
  "summary": { "required": ["port", "power", "heat"], "banned": ["ransomware", "hacker", "malware", "nation-state"] }
}
```

  - `fake-model.ts`: `startFakeModel(port = 5299): Promise<{ close(): Promise<void> }>`. It's an OpenAI-compatible server (`GET /v1/models`, `POST /v1/chat/completions`, streamed SSE with tool calls). It decides from the last message:
    - A user message → call `get_progress`.
    - `[delegated q]` → `record_answer(q, <first listed option, or "fake value"; a one-item array for list>, "proposed", reason "Delegated; a placeholder value")`.
    - A `get_progress` result: `Drafts due:` → `draft(executiveSummary, "A fake summary about the port and its power in the heat.")`. Otherwise the first `- id [...]` line → `ask_user(id, its text, recommended parsed from "recommended: X")`. Otherwise `Gate: passes. Call` → `advance_stage`. Otherwise reply "Please approve on the Questions page."
    - An `advance_stage` or `draft` result → `get_progress`.
    - Anything else → reply "Done."
    - Run as `node evals/fake-model.ts`.
  - `harness.ts`: `runOnce(opts: { apiUrl: string; model: ModelInfo; fixture: Fixture; maxPrompts?: number }): Promise<Observed>`. It plays the person:
    - Create a scenario named `"eval {model} {ISO time}"`, and `prompt(fixture.opening)`.
    - After each prompt settles:
      - **A question was asked:** if the question is in `delegate`, call `delegated(q)`. Otherwise PUT `answers[q]` as a person, then `answered(q, value)`.
      - **No question:** read progress. If `lastStage && gate.passes`, stop. If `gate.approval && !gate.approved` and every finding is `APPROVAL_*`, approve as the owner and call `approved(stage)`. Otherwise `prompt("[continue]")` and count a stall.
    - Three stalls in a row end the run with `stalled: true`. The run also ends at `maxPrompts` (default 150).
    - It records every tool call from `tool_execution_end` (name, arguments, `isError`, `details.code`) with its prompt index, and every final assistant text with its prompt index.
    - It records turns, wall time, input and output tokens (from assistant messages' `usage`), and the time from each prompt to its first `message_update`.
    - It ends with the pulled document.
  - `checks.ts`: `runChecks(o: Observed, fixture: Fixture, kind: any): { id: number; name: string; pass: boolean; detail: string }[]` for checks 1–10:
    1. **Coverage:** the document has 32 decisions and the run didn't stall.
    2. **No repeats:** every successful `ask_user` names a different question.
    3. **Known answers:** no `ask_user` attempt for an `openingQuestions` key.
    4. **One at a time:** no `ALREADY_ASKED`.
    5. **Recommendation:** no `NO_RECOMMENDED`.
    6. **Order:** no `FUTURE_STAGE`.
    7. **Delegation:** each `delegate` question ends `proposed`, with a non-empty reason.
    8. **Fidelity:**
       - Every successful `record_answer` with `who:"stated"` is for an opening question.
       - The value's JSON contains that question's fragments, ignoring case.
       - The quote, whitespace-normalized and ignoring case, is a substring of `opening`.
       - All four opening questions end stated.
    9. **No false claims:** no final text matching `/\b(recorded|saved|noted|updated|stored)\b/i` in a prompt that had no successful `record_answer`, `draft`, `propose_change` or `advance_stage`, and that wasn't an `[answered …]`, `[approved …]` or `[applied …]` note. Those mean the person's own action was saved.
    10. **Summary:** the document's `scenario.executiveSummary` contains every required fragment and none of the banned ones, ignoring case.
  - `report.ts`: `verdict(results: Record<string, CheckResult[][]>, local: string, frontier: string): { pass: boolean; reasons: string[] }`. It implements the pass bar exactly as in Global Constraints. `writeReport(dir, results, metrics)` writes `results-{time}.json` and `report.md`: one table per model (runs × checks), the median metrics, and the verdict.
  - `run.ts`: `--api http://localhost:5280 --models gemma4:26b,sonnet-5.5 --repeat 3 --fixture evals/fixtures/harbor-blackout.json --out evals/results`. It runs the models one after the other, with no parallel runs on the M1.

- [ ] **Step 1: Write the failing tests.**

```ts
// checks.test.ts — hand-built Observed objects
test("check 3 fails when an opening question is asked", ...)
test("check 8 fails when a stated quote isn't in the opening message", ...)
test("check 8 fails when a non-opening question is recorded as stated", ...)
test("check 9 ignores 'recorded' in a prompt that recorded something", ...)
test("verdict fails when sonnet passes a check gemma fails in all three runs", ...)
test("verdict passes when gemma passes all checks in two of three runs", ...)

// harness.test.ts — the API from `dotnet run` on a temp DB, plus startFakeModel()
test("plumbing: the fake model reaches stage 1 and the run records calls", ...)  // runOnce(maxPrompts: 40) → calls include get_progress and ask_user; doc.stage >= 1
test("harness stops after three stalls", ...)   // a streamFn that always replies "ok" with no tool call → stalled === true, at most 4 prompts after the opening
```

  `harness.test.ts` starts the API itself, after `dotnet build api/ScenarioBuilder.slnx`, with `dotnet run --project ../api/ScenarioBuilder.Api --no-build`, `ASPNETCORE_URLS=http://localhost:5281` and `ConnectionStrings__Builder=Data Source=/tmp/sb-eval-{pid}.db`. It kills the API in `afterAll`. The default `fake` model entry already points at `http://localhost:5299/v1`.

- [ ] **Step 2: Run** `npm test -- evals`. Expected: these fail.
- [ ] **Step 3: Implement** the fake model, harness, checks, report and runner. `runOnce` accepts an optional `streamFn` for the stall test.
- [ ] **Step 4: Run** `npm test`. Expected: all agent and eval tests pass.
- [ ] **Step 5: Run** a plumbing run with the fake model (API on 5280, `node evals/fake-model.ts` in another shell): `npm run eval -- --models fake --repeat 1`. Expected: `report.md` is written. The fake fails check 3 (it asks the opening questions), as designed. Checks 4, 5 and 6 pass.

---

### Task 11: The real models, the run, and the UI smoke check

**Files:**
- Create: `tools/litellm.yaml`, `README.md`, `ui/e2e/smoke.mjs` (kept in the repo; it loads playwright-core with `createRequire('/workspaces/GHOSTS/node_modules/')`)

**Interfaces:**
- Consumes: everything above.

- [ ] **Step 1: Get gemma ready. Ask the user first.** Pulling gemma downloads about 17 GB to their Mac. Once they agree:
  - Run `curl http://192.168.65.254:11434/api/pull -d '{"model":"gemma4:26b"}'`.
  - Then `curl http://192.168.65.254:11434/api/create -d '{"model":"gemma4:26b-32k","from":"gemma4:26b","parameters":{"num_ctx":32768}}'`.
  - Point the `gemma4:26b` entry's `Model` at `gemma4:26b-32k`.
  - Expected: `/api/tags` lists both, and `/api/show` for `gemma4:26b-32k` shows `num_ctx 32768`.
- [ ] **Step 2: Get LiteLLM ready.** `tools/litellm.yaml` maps `sonnet-5.5` to `bedrock/us.anthropic.claude-sonnet-5-5`, with `aws_region_name: us-east-1` and `max_tokens: 8192`. Run:

  ```
  docker run -d --name sb-litellm -p 4000:4000 -v $PWD/tools/litellm.yaml:/app/config.yaml:ro -v ~/.aws:/root/.aws:ro -e LITELLM_MASTER_KEY=$LITELLM_KEY ghcr.io/berriai/litellm:main-stable --config /app/config.yaml
  ```

  If `~/.aws` is missing, pass `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` and `AWS_SESSION_TOKEN` with `-e` instead. Expected: `curl -H "Authorization: Bearer $LITELLM_KEY" localhost:4000/v1/models` lists `sonnet-5.5`.
- [ ] **Step 3: Check each model once through the pass-through.** With the API on 5280, run `curl -N localhost:5280/api/model/chat/completions -d '{"model":"gemma4:26b","stream":true,"messages":[{"role":"user","content":"Say hi"}]}'`, then the same with `sonnet-5.5`. Expected: SSE `data:` lines from both.
- [ ] **Step 4: Run the evaluation.** `npm run eval -- --models gemma4:26b,sonnet-5.5 --repeat 3`. This can take hours on the M1. Run it in the background and wait for it to finish rather than polling. Expected: `evals/results/report.md` with six runs and a verdict.
- [ ] **Step 5: UI smoke check.**
  - Setup: the API on 5280, `node evals/fake-model.ts`, and `npx ng serve` on 4310.
  - Run `node ui/e2e/smoke.mjs`, which drives `headless_shell` (path in the memory note: `~/.cache/ms-playwright/chromium_headless_shell-1228/chrome-linux/headless_shell`).
  - Steps:
    1. Create a scenario.
    2. Pick model `fake`.
    3. Send "hello".
    4. Click the card's recommended answer. The `participants` row shows answered.
    5. On the next card, click **You decide**. The row shows "proposed by agent".
    6. Answer the rest of stage 0 through the API as a person, then click **Approve**. The gate passes.
    7. POST a proposal through the API with `X-Via: agent` for `audienceType`, reload, click **Apply**. The row shows the new value.
  - Expected: the script exits 0, with no `pageerror` events.
- [ ] **Step 6: Write the README and report back.**
  - `README.md` covers how to run the API, UI, fake model, LiteLLM and the evaluation, and how to reset (delete `scenario-builder.db`).
  - Then tell the user the verdict and both models' check tables from `report.md`, plus the median wall time per run and time to first token for each model. List every file changed.
