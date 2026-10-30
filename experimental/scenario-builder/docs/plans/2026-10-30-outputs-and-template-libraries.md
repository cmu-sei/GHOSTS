# Outputs and template libraries: implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the scenario's Finish page (export JSON, documents rendered from template libraries,
"send to" stubs), and an Admin area where administrators build, publish, export and import
template libraries.

**Architecture:** The API owns everything that decides what a reader may see:
- the per-question visibility in the kind file;
- an audience filter over the scenario JSON;
- a sandboxed Scriban (Liquid) renderer that wraps HTML in a house page and serves documents under a strict CSP;
- libraries stored as a draft plus numbered versions, each version in the `.library.json` format that export and import use.

The Angular app adds a Finish page, replacing the JSON tab, and three admin screens.

**Tech Stack:** .NET 10, EF Core SQLite, xUnit. Scriban 7.5.3 (`Template.ParseLiquid`, a
`TemplateContext` with `LoopLimit`, `RecursiveLimit` and a `CancellationToken`, and
`Messages[i].Span.Start.Line`, which is 0-based). Markdig 1.5.1. Angular 22 with vitest.

**Spec:** `experimental/scenario-builder/docs/specs/2026-10-30-outputs-and-template-libraries-design.md`

**Git:** the user manages git, so there are no commit steps. Leave the work uncommitted, and list
the changed files at the end.

## Global Constraints

- Everything stays under `experimental/scenario-builder/`. The API remains the only path to a model, and the agent gets no admin abilities.
- Visibility values are `participant`, `excon` and `designer`. Audiences are `participants`, `facilitators` and `designers`. In JSON they're camelCase strings, as all enums are.
- Template formats are `html` and `markdown`. The library file has `"format": "scenario-builder-library"` and `"formatVersion": 1`.
- Render limits: `LoopLimit = 10000`, `RecursiveLimit = 50`, 2 seconds. A template body over 200,000 characters is refused as `TEMPLATE_INVALID`.
- Every rendered document served as HTML carries `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; img-src data:` and `charset=utf-8`.
- Admins come from config `Admins` (a string array, default `["dev"]`). A caller is an admin when it isn't the agent and its user is in the list.
- Send targets: `blueprint`, `cite`, `gallery`, `player`, `steamfitter`, `ghosts`.
- Error bodies stay `{ code, message }`. A template error's message names the template and its 1-based line: "Design summary, line 3: …".
- The schema changes, so `EnsureCreated` needs a fresh database file. Task 9 backs up the running one, as before.
- Copy follows the UI's rules: sentence case, plain verbs, no all-caps labels, no middle-dot metadata strings.

## Review Focus

1. **Exercise-control content in a participant-visible answer.** The executive summary is drafted from ground truth, so a participant document must never carry it. It's `excon` for that reason. *(Task 6: `Participant_read_in_leaves_out_the_summary_that_quotes_ground_truth`.)*
2. **A template that names something that isn't there.** A typo or a not-yet-answered question renders empty instead of failing. *(Task 3: `Missing_values_render_empty`.)*
3. **An oversized template body** is refused, not stored. *(Task 4: `Saving_an_oversized_template_is_refused`.)*
4. **A malformed library file** (the wrong format string, duplicate template ids, a body that won't parse) is refused with the reason. *(Task 4: `Importing_a_bad_file_says_why`.)*
5. **Non-Latin text** in templates and answers comes through HTML and `.md` unchanged. *(Task 6: `Unicode_survives_html_and_markdown`.)*

---

### Task 1: Visibility per question in the kind file

**Files:**
- Modify: `api/ScenarioBuilder.Api/Domain/Kind.cs` (`KindDraft` gains `string? Visibility = null`; validation)
- Modify: `kinds/crucible-tabletop.kind.json`
- Test: `api/ScenarioBuilder.Api.Tests/KindCatalogTests.cs`

**Interfaces:**
- Produces:
  - `public static readonly string[] Kind.Visibilities = ["participant", "excon", "designer"]`
  - `KindQuestion.Visibility` and `KindDraft.Visibility` (null means `participant`)

- [ ] **Step 1: Write the failing tests.**
  - `Tabletop_kind_marks_what_gives_the_game_away`: `obviousExplanation`, `groundTruth`, `motive`, `attackPath`, `ending`, `redHerring`, `escalationCeiling` and `teachingPoints` are `excon`. The `executiveSummary` draft is `excon`. `participants`, `problem` and `objectives` are null.
  - `Unknown_visibility_fails_at_load`: a temp kind with `"visibility": "secret"` throws `InvalidOperationException` naming the question.
- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter KindCatalogTests`. Expected: both fail.
- [ ] **Step 3: Implement.** Add the visibilities to the kind file. Make the validator reject any value outside `Kind.Visibilities`, on questions and on drafts.
- [ ] **Step 4: Run** all API tests. Expected: they all pass. The pull document is unchanged, because list items already carry `excon`.

---

### Task 2: The audience filter

**Files:**
- Create: `api/ScenarioBuilder.Api/Domain/AudienceFilter.cs`
- Test: `api/ScenarioBuilder.Api.Tests/AudienceFilterTests.cs`

**Interfaces:**
- Produces:
  - `public enum Audience { Participants, Facilitators, Designers }`
  - `public static JsonObject AudienceFilter.For(JsonObject document, Kind kind, Audience audience)` returns a filtered deep copy and never changes its input. It applies, in order:
    1. Remove `decisions` and `findings` unless the audience is designers.
    2. Remove each question's and each draft's path when its visibility is above the audience. Rank `participant` 0, `excon` 1, `designer` 2. Participants may see rank ≤ 0, facilitators ≤ 1, designers ≤ 2.
    3. Remove every array item that is an object with a `visibility` above the audience.
    4. Remove objects and arrays left empty, recursively, without removing the top level.

- [ ] **Step 1: Write the failing tests.** Build the document with `ScenarioDocument.Build` from a scenario seeded at stage 3: every stage answered, `groundTruth` set to `["The generators failed their test"]`, `motive` to `"Fees"`, and the summary drafted.
  - `Participants_lose_the_ledger_ground_truth_and_excon_answers`: there's no `decisions`, `findings`, `groundTruth`, `teachingPoints`, `scenario.motive`, `scenario.obviousExplanation`, or `scenario.executiveSummary`. `scenario.problem`, `objectives` and `engagement.participants` remain. `doc.ToJsonString()` doesn't contain "generators failed".
  - `Facilitators_keep_exercise_control_material_but_not_the_ledger`: `groundTruth[0].text` is present. `decisions` is absent.
  - `Designers_get_everything`: the output equals the input, by `JsonNode.DeepEquals`.
  - `Emptied_containers_are_dropped`: for a hand-built document with only `scenario.motive` under `scenario`, participants get no `scenario` key.
  - `The_input_is_left_alone`: after filtering for participants, the original still has `decisions`.
- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter AudienceFilterTests`. Expected: the build fails.
- [ ] **Step 3: Implement.** Use `DeepClone()`, then remove the parts by path (`/a/b` split on `/`).
- [ ] **Step 4: Run** the same tests. Expected: they all pass.

---

### Task 3: The template renderer

**Files:**
- Modify: `api/ScenarioBuilder.Api/ScenarioBuilder.Api.csproj` (add `Scriban` 7.5.3 and `Markdig` 1.5.1)
- Create: `api/ScenarioBuilder.Api/Domain/TemplateRenderer.cs`
- Test: `api/ScenarioBuilder.Api.Tests/TemplateRendererTests.cs`

**Interfaces:**
- Produces:
  - `public enum TemplateFormat { Html, Markdown }`
  - `public record TemplateSpec(string Id, string Name, Audience Audience, TemplateFormat Format, string Body)`
  - `public record RenderInput(JsonObject Scenario, IReadOnlyDictionary<string, string> Labels, Kind Kind, string ScenarioName, string LibraryName, int LibraryVersion, int OpenFindings, DateTime Generated)`. `Scenario` is already filtered, and `draft` is `OpenFindings > 0`.
  - `public sealed class TemplateError(string template, int line, string message) : Exception`. Its message reads `"{template}, line {line}: {message}"`, and line 0 means not tied to a line.
  - `static string? TemplateRenderer.Check(TemplateSpec t)` returns null, or the error message for a parse error or a body over 200,000 characters.
  - `static string TemplateRenderer.Render(TemplateSpec t, RenderInput input)` returns the Liquid output, or throws `TemplateError`.
  - `static string TemplateRenderer.Page(TemplateSpec t, RenderInput input, string rendered)` returns a full HTML page.
  - `static string TemplateRenderer.MarkdownFile(RenderInput input, string rendered)` returns the `.md` text.
- The model given to Liquid: `scenario` (the JSON converted to `ScriptObject` and `ScriptArray`), `labels`, `kind` (`id`, `name`, `version`), `library` (`name`, `version`), `template` (`name`, `audience`), `generated` (ISO 8601), and `draft`. For `Html`, every string, the labels included, goes through `WebUtility.HtmlEncode` first.
- The house page: `<!doctype html>`, `<meta charset="utf-8">`, `<title>{name} for {scenario}</title>`, an inline print stylesheet (system sans stack, 72ch measure, `@page` margins), then:
  - `<div class="draft">Draft: {n} findings open.</div>` while draft;
  - the content;
  - `<footer>Made from {library}, version {n}, on {d MMMM yyyy}.</footer>`.

  Markdown goes through `Markdig.Markdown.ToHtml` first. If the rendered HTML already contains `<html` (ignoring case), the banner goes in after `<body…>`, and the page is otherwise left alone.
- The `.md` file: `> Draft: {n} findings open.\n\n` while draft, then the rendered text.

- [ ] **Step 1: Write the failing tests.**
  - `Renders_liquid_against_the_scenario_and_labels`: `"{{ scenario.name }} {% for o in scenario.objectives %}[{{ o.text }}]{% endfor %} {{ labels.timeSlot }}"` renders as `"Harbor [Assess] Time slot"`.
  - `Html_values_are_escaped`: a value `"<b>x</b>"` renders as `"&lt;b&gt;x&lt;/b&gt;"`. `Markdown_values_are_raw`: it renders as `"<b>x</b>"`.
  - `Missing_values_render_empty`: `"[{{ scenario.nope.deeper }}]"` renders as `"[]"`.
  - `Parse_error_names_the_template_and_line`: the body `"ok\n{% for x in y %}"` makes `Check` return a message containing `"line 2"`.
  - `Loop_limit_stops_a_runaway_template`: `{% for i in (1..100000) %}x{% endfor %}` throws `TemplateError`.
  - `Oversized_body_is_refused`: 200,001 characters make `Check` return a message containing `"200,000"`.
  - `House_page_has_title_banner_and_footer`: it contains `<title>Brief for Harbor</title>`, `Draft: 2 findings open.`, `Made from Basics, version 3, on`, and `charset="utf-8"`. With `OpenFindings = 0` there's no `Draft:`.
  - `A_full_page_template_keeps_its_own_page_and_gets_the_banner`.
  - `Markdown_preview_is_rendered_and_the_file_starts_with_the_draft_line`.
- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter TemplateRendererTests`. Expected: the build fails.
- [ ] **Step 3: Implement.** Convert `JsonNode` to Scriban values. Use `new TemplateContext { LoopLimit = 10000, RecursiveLimit = 50, StrictVariables = false }`, `PushGlobal`, and `CancellationToken` from a 2-second source. Map a parse error to `Messages[0].Span.Start.Line + 1`. Map `ScriptRuntimeException` and `OperationCanceledException` to `TemplateError`.
- [ ] **Step 4: Run** the same tests. Expected: they all pass.

---

### Task 4: Libraries and the admin API

**Files:**
- Modify: `Domain/Entities.cs` (`TemplateLibrary`, `LibraryVersion`), `Infrastructure/BuilderDb.cs` (DbSets, a unique slug index), `appsettings.json` (`"Admins": ["dev"]`), `Endpoints/Caller.cs` (`static bool IsAdmin(Caller, IConfiguration)`)
- Create: `Domain/LibraryFile.cs`, `Endpoints/Me.cs`, `Endpoints/Admin/{Libraries,Library,Publish,Export,Import,Preview}.cs`
- Test: `api/ScenarioBuilder.Api.Tests/AdminTests.cs`

**Interfaces:**
- Produces:
  - `record LibraryFile(string Format, int FormatVersion, string Slug, string Name, string? Description, List<string> Kinds, int Version, List<TemplateSpec> Templates)`, with `static string? LibraryFile.Check(LibraryFile f)`. It checks the format and its version, a kebab-case slug, that the kinds are known, that template ids are unique and kebab-case, that names are non-empty, and `TemplateRenderer.Check` on each template.
  - `TemplateLibrary { Id, Slug, Name, Description, Kinds (JSON), Draft (JSON templates), Created, Updated }`
  - `LibraryVersion { Id, LibraryId, Number, Content (LibraryFile JSON), PublishedBy, Published }`
  - `static LibraryVersion? Latest(...)` (a helper)
  - The endpoints in spec §10. `ApiFactory` gains `Admins` defaulting to `dev`.
  - `POST /api/admin/preview` returns `text/html` with the CSP header.
  - **One import for both uses:** `static async Task<TemplateLibrary> LibraryImport.Apply(BuilderDb db, LibraryFile file, string by, CancellationToken ct)`. Startup seeding (Task 5) and the import endpoint both call it.

- [ ] **Step 1: Write the failing tests.**
  - `Only_admins_manage_libraries`: `Client(user: "lee")` gets `403 NOT_ADMIN` on GET `/api/admin/libraries`, and `Client(agent: true)` does too.
  - `Me_says_who_is_an_admin`: `dev` gets `admin: true`, and `lee` gets `false`.
  - `Create_save_and_publish_numbers_versions`: create, save one template, publish (v1), change the body, publish (v2). The list shows `latestVersion 2` and `draftChanged: false`. After another body change, `draftChanged: true`.
  - `Publishing_an_unchanged_draft_is_refused`: `409 NOTHING_TO_PUBLISH`.
  - `Saving_an_invalid_template_says_where`: `400 TEMPLATE_INVALID` with a message containing `"line 2"`.
  - `Saving_an_oversized_template_is_refused`.
  - `Export_then_import_round_trips_into_another_installation`: export from one factory, import into a fresh one, and the templates come out equal.
  - `Importing_an_existing_slug_adds_a_version`.
  - `Importing_a_bad_file_says_why`: a wrong `format` gives `400 LIBRARY_INVALID` "format", and duplicate ids give "used twice".
  - `Preview_renders_as_the_audience_would_see_it`: a participants template `{{ scenario.groundTruth }}|{{ scenario.problem }}`, previewed on a seeded scenario, contains the problem and not the ground truth. The response carries the CSP header.
- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter AdminTests`. Expected: these fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all API tests. Expected: they all pass.

---

### Task 5: The starter library

**Files:**
- Create: `libraries/crucible-tabletop-basics.library.json` (three templates per spec §11), `Infrastructure/LibrarySeeder.cs`
- Modify: `Program.cs` (seed after `EnsureCreated`, from `Libraries:Directory`, default `../../libraries`), `ApiFactory.cs` (`Libraries:Directory` set to the repo folder)
- Test: `api/ScenarioBuilder.Api.Tests/StarterLibraryTests.cs`

**Interfaces:**
- Consumes: `LibraryImport.Apply` from Task 4, with `by: "system"`.
- The templates:
  - `design-summary`: designers, html. Sections for engagement, objectives, teaching points, premise (problem, obvious explanation, actor, motive, way in, ceiling, ending, red herring, symmetry), ground truth, names and vocabulary, the executive summary, then a decisions table (label, value, who, why).
  - `executive-brief`: facilitators, markdown.
  - `participant-read-in`: participants, markdown.

  Each uses `labels`, and guards optional parts with `{% if %}`.

- [ ] **Step 1: Write the failing tests.**
  - `Starter_library_loads_into_an_empty_installation`: the list has `crucible-tabletop-basics` at version 1.
  - `Starter_library_loads_only_once`: two hosts on one database file still list one library at version 1.
  - `Every_starter_template_renders_for_a_finished_scenario`: render each through the documents endpoint (Task 6 creates it; until then, through `TemplateRenderer` directly) for a stage 3 scenario. There are no errors, and each output names the exercise.
- [ ] **Step 2: Run.** Expected: these fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all API tests. Expected: they all pass.

---

### Task 6: A scenario's outputs API

**Files:**
- Create: `Endpoints/Outputs/{Export,Documents,Document,Send}.cs`
- Test: `api/ScenarioBuilder.Api.Tests/OutputsTests.cs`

**Interfaces:**
- Produces:
  - `record DocumentView(string Library, string LibraryName, int Version, string Template, string Name, Audience Audience, TemplateFormat Format)`
  - The endpoints in spec §10.
  - A document that fails to render returns `422`, with an HTML page naming the template and line.
  - HTML responses set the CSP header.
  - Downloads set `Content-Disposition: attachment; filename="{slug}-{template}.{html|md}"`, and the JSON export uses `"{slug}.scenario.json"`.

- [ ] **Step 1: Write the failing tests.**
  - `Export_is_the_pull_document_as_an_attachment`: the file name is `harbor-blackout.scenario.json`, and the body deep-equals `GET /api/scenarios/{id}`.
  - `Documents_lists_the_starter_templates_for_the_kind`: three documents, `crucible-tabletop-basics` version 1.
  - `Participant_read_in_leaves_out_the_summary_that_quotes_ground_truth`: draft a summary containing "generators failed" and set the same text as ground truth. The participant read-in contains neither. The design summary contains both.
  - `Markdown_download_is_raw_with_the_draft_line`: `text/markdown`, starting `> Draft:` while findings are open.
  - `Documents_carry_the_content_security_policy`.
  - `Unicode_survives_html_and_markdown`: an answer of `"Données — 記録"` appears unchanged in both.
  - `Every_send_target_is_not_connected_yet`: the six targets give `501 NOT_CONNECTED` with the target's name in the message, and `"nope"` gives `404 TARGET_UNKNOWN`.
  - `A_broken_published_template_gives_an_error_page`: import a library whose template fails at runtime (a loop over `(1..100000)`), and rendering it gives `422`, naming the template.
- [ ] **Step 2: Run** `dotnet test api/ScenarioBuilder.slnx --filter OutputsTests`. Expected: these fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** all API tests. Expected: they all pass.

---

### Task 7: The Finish page

**Files:**
- Modify: `ui/src/app/core/models/builder.model.ts` (`DocumentView`), `core/services/builder-api.service.ts` (`getDocuments`, `send`, `documentUrl`, `exportUrl`), `features/workspace/workspace.routes.ts` (`finish`, plus `json` redirecting to `finish`), `features/workspace/scenario-shell/scenario-shell.component.ts` (tabs: Questions, Summary, Decision log, Finish)
- Create: `features/workspace/finish-page/finish-page.component.{ts,html,scss,spec.ts}`
- Delete: `features/workspace/json-page/json-page.component.ts`. Its JSON view moves into Finish.

**Interfaces:**
- Consumes: Task 6's endpoints.
- Produces:
  - `documentUrl(id, library, template, download = false): string` returns `/api/scenarios/{id}/documents/{library}/{template}` with `?download=true` when downloading.
  - `exportUrl(id): string`.
  - `send(id, target): Observable<never>`. The panel shows the error's message, because every target answers 501.

- [ ] **Step 1: Write the failing spec** `finish-page.component.spec.ts`, using a stub API:
  - `lists each document with its audience, format and library`
  - `links Preview and Download to the document URLs`
  - `shows the JSON on Show JSON`
  - `says "not connected yet" when a Send to button is pressed`
  - `points admins to the admin area when no library serves the kind`
- [ ] **Step 2: Run** `npx ng test --watch=false` in `ui/`. Expected: these fail.
- [ ] **Step 3: Implement.** Use the existing look: `sheet`, `sb-button`, the hairline list. **Download JSON** and **Download** are `<a download>` links, so the browser saves the file.
- [ ] **Step 4: Run** the UI tests. Expected: they all pass.

---

### Task 8: The admin area

**Files:**
- Create: `ui/src/app/core/services/admin-api.service.ts`, `core/services/me.service.ts` (cached `/api/me`), `features/admin/admin.routes.ts`, and `features/admin/{libraries-page,library-page,template-editor}/*` (component, template, styles, spec)
- Modify: `app.routes.ts` (`admin`), `shared/components/top-bar/top-bar.component.ts` (an **Admin** link when `me.admin`)

**Interfaces:**
- Consumes: Task 4's endpoints.
- Produces:
  - `AdminApiService`: `libraries()`, `create(name, description, kinds)`, `library(id)`, `save(id, library)`, `publish(id)`, `exportUrl(id)`, `import(file: LibraryFile)`, `preview(scenarioId, audience, format, body): Observable<string>`.
  - `MeService.me: Signal<{ user: string; admin: boolean } | null>`.
- The template editor:
  - A code textarea (monospace is right here, since it's code).
  - Selects for audience and format, and a scenario picker.
  - A preview in `<iframe sandbox="" [srcdoc]>`, refreshed 400 ms after typing stops.
  - A `TEMPLATE_INVALID` message, shown with its line, in place of the preview.
  - **Save** writes the whole draft. Unsaved changes are marked.

- [ ] **Step 1: Write the failing specs.**
  - libraries page: `lists libraries with version and draft state`, `imports a chosen .library.json file`.
  - library page: `publishes and shows the new version`, `lists templates with audience and format`.
  - template editor: `shows a template error with its line instead of the preview`, `marks unsaved changes`.
  - top bar: `shows Admin only to admins`.
- [ ] **Step 2: Run.** Expected: these fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the UI tests. Expected: they all pass.

---

### Task 9: Smoke check, the running instance, and a look

**Files:**
- Modify: `ui/e2e/smoke.mjs` (new steps after "apply a proposal")

- [ ] **Step 1: Extend the smoke check.** Open **Finish**. The three starter documents are listed. Preview the participant read-in in a new tab and check it has no "ground truth" heading. Download the JSON and check its `slug`. Press **Send to Blueprint** and see "isn't connected yet". Open **Admin**, see the starter library, and open its template editor; the preview renders.
- [ ] **Step 2: Restart the running instance.** Back up `api/ScenarioBuilder.Api/scenario-builder.db` with a dated name, restart the API on :5280, and confirm the starter library is listed.
- [ ] **Step 3: Run the checks.** Run the full API, agent and UI suites, a production build in a `/tmp` copy, and the smoke check against a scratch API. Take screenshots of Finish, the libraries list and the template editor for a self-critique.
