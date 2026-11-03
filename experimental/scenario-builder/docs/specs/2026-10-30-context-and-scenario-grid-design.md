# Context sources and the scenario grid: design

**Date:** 2026-10-30 · **Status:** building (the user said "go") · **Builds on:** [2026-10-29-scenario-builder-design.md](2026-10-29-scenario-builder-design.md)

Two asks from the user:

- "I should be able to provide context at the beginning of a scenario, when I create it. Like the one in GHOSTS: text, URLs, or upload docs."
- "In the list of existing scenarios, make it a grid so I can search, sort, and archive them."

## 1. Decisions

| # | Topic | Decision |
|---|---|---|
| 1 | Where | A new scenario opens on a **Context** page, the first tab of its top bar. Sources can be added or removed there at any time; existing scenarios still open on Questions. |
| 2 | Kinds of source | Pasted text; a URL the server fetches; an uploaded file. As in GHOSTS (`ScenarioSourceService`, `ScenarioBuilderController.ExtractText`). |
| 3 | File types | PDF (PdfPig), DOCX (OpenXml), and text: `.txt`, `.md`, `.csv`, `.json`, `.html`/`.htm` (tags stripped). Anything else is refused with the list. A file with no text (a scanned PDF) is refused, telling the person to paste its text. |
| 4 | URLs | `http`/`https` only, fetched by the API with a 20 s timeout and a 5 MB cap. HTML is stripped to text; a PDF link is read as a PDF. A failed fetch says why: on a closed range, paste the text instead. |
| 5 | Limits | 20 MB per file, 1,000,000 characters of text per source. |
| 6 | Storage | One `Source` row per source: kind, name, origin (the URL or file name), the extracted text, who added it, when. The file itself isn't kept. |
| 7 | Who | Any person can add or remove a source, as any person can answer. The agent can't add or remove one; it only searches them. |
| 8 | How the agent uses them | `get_progress` lists the sources. A new tool, `search_sources(query)`, returns up to four passages (about 1,200 characters each) ranked by how often they use the query's words. No embeddings, so it works with any local model and no extra service. The prompt says: search before recommending, prefer what the sources say, and name the source in the reason. |
| 9 | Ledger | Unchanged. An answer drawn from a source is the agent's proposal, with the source in its reason; only the person's own words are stated. |
| 10 | Grid | The scenarios list becomes a table: search (name, kind, owner), sort by any column (name, kind, stage, owner, updated), and Archive. Archived scenarios are hidden unless **Show archived** is on. |
| 11 | Archive | `PUT /api/scenarios/{id}/archive { archived }`, by the owner as a person, like delegation. Archiving hides a scenario from the list; nothing else changes, and the pull API still serves it. |
| 12 | Schema | Changes (`Source`, `Scenario.Archived`), so the running database is backed up and started fresh, as before. |

## 2. API

| Endpoint | Does |
|---|---|
| `GET /api/scenarios/{id}/sources` | The sources, without their text: id, kind, name, origin, characters, added by, added. |
| `POST /api/scenarios/{id}/sources/text` | `{ name, text }`. |
| `POST /api/scenarios/{id}/sources/url` | `{ url, name? }`. The page's title names it when no name is given. `422 URL_UNREACHABLE` with the reason. |
| `POST /api/scenarios/{id}/sources/file` | Multipart `file`. `415 SOURCE_UNSUPPORTED`, `422 SOURCE_EMPTY`, `413 SOURCE_TOO_LARGE`. |
| `DELETE /api/scenarios/{id}/sources/{sourceId}` | Removes it. |
| `GET /api/scenarios/{id}/sources/search?q=` | Up to four passages: source id and name, passage number and count, text. |
| `PUT /api/scenarios/{id}/archive` | `{ archived }`. Owner, as a person. |

`GET /api/scenarios` gains `created` and `archived`; `GET /api/scenarios/{id}/progress` gains `sources`.

## 3. Out of scope

- Spreadsheets (`.xlsx`), images and OCR.
- Fetching a URL through a headless browser (GHOSTS tries Playwright first); the API's plain fetch reads static pages.
- Embeddings or a vector index.
- Entity extraction from sources (GHOSTS's extraction runner).
