# Outputs and template libraries: design

**Date:** 2026-10-30 · **Status:** for review · **Builds on:** [2026-10-29-scenario-builder-design.md](2026-10-29-scenario-builder-design.md)

A scenario needs to leave the builder in several forms: as its JSON, as documents people read, and
eventually as packages for the Crucible apps and GHOSTS. This piece adds two things:

- a **Finish** page at the end of each scenario, for the JSON, documents, and "send to" buttons;
- an **Admin** area, where administrators build **template libraries** that decide which documents
  each kind of scenario offers.

## 1. Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Template format | HTML or Markdown with Liquid placeholders. No Word or other binary formats, ever. |
| 2 | Outputs | HTML, which the person saves as PDF from the browser, and Markdown `.md` files. |
| 3 | Placeholder language | Liquid, through Scriban, sandboxed: loop, recursion and time limits, no file or network access. |
| 4 | Admins | A configured list of usernames, as the auth proxy reports them (`X-Forwarded-User`). The default is `dev`. The agent never has admin rights. |
| 5 | What a template sees | Each template declares an audience. Before filling it, the builder removes everything that audience may not see (§5). No template mistake can leak exercise-control material to participants. |
| 6 | Versions | Editing a library edits its draft. Publish makes the next numbered version. Documents use the latest published version and name it in their footer. |
| 7 | Rendering | On demand, never stored. A document can't drift from its scenario. |
| 8 | Never blocked | Documents can be made at any stage. While a scenario has open findings, each one carries a draft banner (spec 2026-10-29, decision 12). |
| 9 | Moving libraries | A library version exports to one `.library.json` file and imports into another installation, so a library can be carried onto a closed range. |
| 10 | Placement | **Finish** is the last tab of the scenario's top bar, replacing the JSON tab. The JSON moves into Finish. |
| 11 | "Send to" | Buttons for Blueprint, CITE, Gallery, Player, Steamfitter and GHOSTS. Each calls a real endpoint that answers "not connected yet", so connecting one later changes only the server. |
| 12 | Starter library | "Crucible tabletop basics" ships with the repository and loads when an installation has no libraries. |

## 2. Words used here

| Word | Meaning |
|---|---|
| Template | One document's Liquid source, with a name, an audience and a format (`html` or `markdown`). |
| Library | A named set of templates, for one or more kinds, with a draft and numbered published versions. |
| Audience | Who a document is for: `participants`, `facilitators` or `designers`. |
| Visibility | Who may see a piece of the scenario: `participant` (everyone), `excon` (facilitators and designers), or `designer` (designers only). |

## 3. The Finish page

The scenario's top bar reads Questions, Summary, Decision log, **Finish**. Finish has three sections.

- **Export.** **Download JSON** saves `<slug>.scenario.json`, byte for byte what the pull API returns.
  It's marked `"status": "draft"` and carries its open findings. **Show JSON** opens the same document
  in place, as the JSON tab did.
- **Documents.** One row per template in the latest published version of every library assigned to
  the scenario's kind. Each row shows:
  - the template's name, audience and format;
  - the library and its version;
  - **Preview**, which opens the rendered document in a new tab (Markdown is shown rendered);
  - **Download**, which saves `.html` or `.md`.

  When no library serves the kind, the section says so, and tells admins where to make one.
- **Send to.** Blueprint, CITE, Gallery, Player, Steamfitter, GHOSTS. Each says "Sending to Blueprint
  isn't connected yet." when pressed.

## 4. Admin

Admins see an **Admin** link in the top bar. `/api/me` tells the UI whether the user is one.

- **Libraries** (`/admin`). A list showing each library's name, its kinds, its latest version, and
  "Draft changes" when the draft differs from that version. It has **New library** and **Import**
  (choose a `.library.json` file).
- **A library** (`/admin/libraries/:id`).
  - Name, description, and the kinds it serves (checkboxes from the loaded kinds).
  - Its templates, with **Add template**. Edits save to the draft.
  - **Publish**, which makes the next version.
  - **Export**, which downloads the latest published version.
  - A read-only list of versions: number, who published it, when.
- **A template** (`/admin/libraries/:id/templates/:templateId`). Name, audience, format, and the body
  in a code editor. A live preview sits beside it: pick a scenario, and see the template rendered as
  its audience would get it. A template error shows its message and line in place of the preview.

## 5. What each audience may see

The filter runs on the server, on the scenario JSON (the pull API's document), before a template
receives it.

1. **The ledger is for designers only.** `decisions` and `findings` are removed for participants and
   facilitators.
2. **Each question's visibility comes from the kind file.** A question with `"visibility": "excon"` is
   removed at its path for participants. A question with `"visibility": "designer"` is removed for
   participants and facilitators. A question with no visibility is `participant`. Drafts carry a
   visibility the same way.
3. **List items keep their own labels.** An item labelled `excon` or `designer` is removed the same way.
   Today these are ground truth and teaching points.
4. **A container left empty by the filter is removed**, so templates can test whether something exists.

This piece sets these visibilities in `crucible-tabletop.kind.json`. These answers say what is really
happening, which participants work out during the exercise:

| Question or draft | Visibility |
|---|---|
| `obviousExplanation` (it includes what is really happening), `groundTruth`, `motive`, `attackPath`, `ending`, `redHerring`, `escalationCeiling`, `teachingPoints` | `excon` |
| `executiveSummary` (draft) | `excon` |
| everything else | `participant` |

The kind loader rejects any other visibility value.

## 6. What a template receives

| Name | Holds |
|---|---|
| `scenario` | The filtered scenario JSON, for example `scenario.engagement.participants` or `scenario.objectives`. |
| `labels` | Each question's short label by id, for example `labels.timeSlot`. |
| `kind` | `id`, `name`, `version`. |
| `library` | `name`, `version`. |
| `template` | `name`, `audience`. |
| `generated` | The time of rendering, ISO 8601, UTC. |
| `draft` | `true` while the scenario has open findings. |

**HTML templates get escaped values.** Every string taken from the scenario is HTML-escaped before an
HTML template receives it, so an answer containing `<script>` prints as text. Markdown templates
receive values as they are.

## 7. Rendering

- **HTML.** If the rendered text has no `<html` element of its own, it's wrapped in a house page:
  - a print stylesheet;
  - the title "<template> for <scenario>";
  - a draft banner while `draft` is true;
  - a footer reading "Made from <library>, version <n>, on <date>."

  A template that writes its own full page gets only the draft banner.
- **Markdown.** A download is the rendered text, with a first line `> Draft: <n> findings open.`
  while draft. A preview renders the Markdown to HTML (Markdig) in the house page.
- **Served safely.** Rendered documents are served with
  `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'; img-src data:` so a
  template's markup can't run script in the builder's origin. The admin preview shows its HTML in a
  sandboxed iframe.
- **Limits.** Loops are capped at 10,000 iterations, recursion at 50, and rendering at 2 seconds.
  Going over is a template error.

## 8. The library file

`.library.json` holds one version of one library:

```json
{
  "format": "scenario-builder-library",
  "formatVersion": 1,
  "slug": "crucible-tabletop-basics",
  "name": "Crucible tabletop basics",
  "description": "…",
  "kinds": ["crucible-tabletop"],
  "version": 1,
  "templates": [
    { "id": "design-summary", "name": "Design summary", "audience": "designers", "format": "html", "body": "…" }
  ]
}
```

**Import** reads a file:
- If no library has its slug, it becomes a new library, and the file's templates become version 1.
- If a library with that slug exists, the file's templates become that library's next version.

Either way the draft is reset to the imported templates.

## 9. Data

| Table | Holds |
|---|---|
| `TemplateLibrary` | id, slug (unique), name, description, kinds (JSON), draft templates (JSON), created, updated |
| `LibraryVersion` | id, library, number, content (the library file's JSON), published by, published |

The schema changes, and storage still has no migrations (spec 2026-10-29, §7). So the running
installation's database is backed up and started fresh, as it was for delegation.

## 10. API

**Admin.** Admins only. The agent is always refused with `403 NOT_ADMIN`.

| Endpoint | Does |
|---|---|
| `GET /api/me` | `{ user, admin }`. Open to everyone. |
| `GET /api/admin/libraries` | Lists the libraries: id, slug, name, kinds, latest version, whether the draft has changes. |
| `POST /api/admin/libraries` | `{ name, description, kinds }` creates a library with an empty draft. |
| `GET /api/admin/libraries/{id}` | Returns the library, its draft templates, and its versions. |
| `PUT /api/admin/libraries/{id}` | `{ name, description, kinds, templates }` saves the draft. Each template's body is parsed, and a parse error returns `400 TEMPLATE_INVALID` with the template, message and line. |
| `POST /api/admin/libraries/{id}/publish` | Makes the draft the next version. Returns `409 NOTHING_TO_PUBLISH` when the draft matches the latest version. |
| `GET /api/admin/libraries/{id}/export` | The latest version as a `.library.json` attachment. Returns `409 NOT_PUBLISHED` before the first publish. |
| `POST /api/admin/libraries/import` | Imports a library file (§8). Returns `400 LIBRARY_INVALID` for a file that isn't one. |
| `POST /api/admin/preview` | `{ scenarioId, audience, format, body }` returns the rendered document, or `400 TEMPLATE_INVALID` with the line. |

**A scenario's outputs.** Open to everyone.

| Endpoint | Does |
|---|---|
| `GET /api/scenarios/{id}/export` | The pull API's JSON as `<slug>.scenario.json`. |
| `GET /api/scenarios/{id}/documents` | The documents on offer, each with its library, version, template, name, audience and format. |
| `GET /api/scenarios/{id}/documents/{library}/{template}` | The rendered document as HTML (Markdown rendered for reading). `?download=true` returns an attachment instead: `.html`, or the raw `.md`. |
| `POST /api/scenarios/{id}/send/{target}` | `501 NOT_CONNECTED` for `blueprint`, `cite`, `gallery`, `player`, `steamfitter` and `ghosts`. Returns `404 TARGET_UNKNOWN` for anything else. |

## 11. The starter library

`libraries/crucible-tabletop-basics.library.json` holds three templates. They use only what stages
0 to 3 produce today.

| Template | Audience | Format | Contents |
|---|---|---|---|
| Design summary | designers | html | Every decision, with who made it and why. Objectives, teaching points, the premise and ground truth, names and vocabulary, and the executive summary. |
| Executive brief | facilitators | markdown | The executive summary, the problem, the obvious explanation and ground truth, the escalation ceiling, and the objectives with teaching points. |
| Participant read-in | participants | markdown | The engagement (who, when, plain English), the problem, what is real and fictional, the organizations, and the objectives. The filter guarantees no ground truth reaches it. |

At startup, an installation with no libraries imports every file in `Libraries:Directory` (default
`../../libraries`) as published version 1, by `system`.

## 12. When things go wrong

- **A template error.** On save and in the preview it returns `TEMPLATE_INVALID`, with the template's
  name, the message and the line. On a scenario's Documents list, a broken published template
  renders an error page naming the template and its line. It doesn't break the other templates.
- **A template that loops forever, or runs too long,** is stopped by the limits in §7.
- **A library with no published version** offers no documents. Its admin page says "Publish to offer
  these templates."

## 13. How we'll know it works

| What | Covers |
|---|---|
| API | A participant render never contains ground truth, motive, teaching points or the ledger, even when its template asks for them. A facilitator render has ground truth but no ledger. HTML templates escape values. Only admins manage libraries. Publish numbers versions and refuses an empty publish. Export then import round-trips a library and adds a version to an existing slug. The starter library loads only into an empty installation. Every send target answers `NOT_CONNECTED`. A template error returns its line. The loop limit stops a runaway template. |
| UI | The Finish page lists documents and Send to buttons, and shows the JSON. The admin list, library editor and template editor work, with preview errors shown. The Admin link shows only for admins. |
| Browser | The smoke check is extended: open Finish, preview a starter document, download the JSON, press a Send to button. |

## 14. Out of scope

- **Word and other binary formats:** never (decision 1).
- **Server-side PDF:** the browser's Save as PDF covers it. A later piece could add it.
- **Real Crucible and GHOSTS exports:** the stubs are in place for them.
- **Pinning a library version per scenario:** documents use the latest published version and name it.
- **An agent that drafts templates:** the separate, reviewed template path from spec 2026-10-29,
  decision 4, comes later.
- **Deleting libraries, and per-library permissions.**
