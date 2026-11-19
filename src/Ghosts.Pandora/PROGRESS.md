# Pandora Modernization: Progress

Working log for the multi-session effort to make Pandora a coherent synthetic information
environment. The audit that set the priorities is in [AUDIT.md](AUDIT.md).

## Increment 1 (2026-11-19): correctness, persistence, one shared world

### Done

| Audit # | Change | Main files |
|---------|--------|------------|
| 1 | Word and PowerPoint files were 0 bytes. The package is now disposed before the stream is read. PowerPoint gets a theme, a master, a layout and a slide list. Both formats pass `OpenXmlValidator`. | `Services/OfficeDocumentGenerationService.cs` |
| 2, 3 | Stored and real-time XSS. Messages render through `MessageHtml`, which encodes everything except Pandora's own upload `<img>`. Hub broadcasts send the encoded text, and the JS templates escape the username. | `Infrastructure/MessageHtml.cs`, 20 views, `social.js`, `posts.js`, `PostsHub.cs` |
| 4, 5 | Generated content is stored per URL. `ContentManager` was rewritten: plain-segment paths only, a containment check, temp file then rename, per-key locks and a size cap. | `Services/ContentManager.cs`, `PandoraController.cs` |
| 6 | Requests are interpreted. `ContentRequest` reads kind, title, department, year and quarter from the path. `DocumentComposer` writes structured reports about one seeded `World` (organization, staff, servers, projects, vendors). Word, PDF, PowerPoint, text, Excel, CSV and JSON all render the same model. | `Infrastructure/Content/*` |
| 7 | Website mode is a real site. Articles and products have stable IDs, every section, article, About, Contact, Privacy and Terms link resolves, and unknown paths get a themed 404. | `Services/WebsiteGenerationService.cs`, `HomeController.cs` |
| 8 | An unknown or crafted theme falls back to the default instead of returning a 500 or reaching a view path. | `BaseController.cs`, `ThemeViewLocationExpander.cs`, theme-reading controllers |
| 9 | Payload mappings answer at their configured URL. | `PandoraFileExtensionMiddleware.cs`, `PayloadController.cs` |
| 10 | `.doc`, `.xls` and `.ppt` requests download as `.docx`, `.xlsx` and `.pptx`, the format they really are. | `PandoraController.cs` |
| 11 | `ApplicationConfiguration` binds from `builder.Configuration`, so `appsettings.{Env}.json`, the command line and host or test settings now apply. | `Program.cs`, `ApplicationConfiguration.cs` |
| new | The extension middleware's prefix check matched raw strings, so `/it/x.pdf` (`/i`), `/documents/x.pdf` (`/doc`) and `/files/x.xlsx` never reached the right generator. It now compares whole first segments. | `PandoraFileExtensionMiddleware.cs` |
| new | Aspire defines a resource for every social theme and website type on ports 6360–6371, `facebook` included. All use explicit start, so they stay off until started from the dashboard. Everything that pointed at the old `facebook` port 8800 now uses 6360: n8n `PANDORA_URL`, the API NPC chat `SocialHost`, client-3's timeline, the development workflows, the horde scripts and the docs. | `src/apphost/AppHost.cs` and the references |
| new | Report tables match the spreadsheet, CSV and JSON for the same path (minutes, project status and assessment findings drew their rows from a different point in the seed). | `Content/DocumentComposer.cs` |
| new | `GET /api/posts/{id}/detail` returned a 500 for any post with a like or comment (JSON cycle `Like.Post → Post.Likes`). | `Models/Like.cs`, `Models/Comment.cs` |

### Decisions

- **Determinism first, storage second.** All document content derives from `hash(path without extension) ^ world seed`. That makes storage a cache rather than the only source of stability, makes concurrent first requests agree, and makes `World:Seed` reproduce a scenario. Faker.Net is process-global and cannot be seeded per request, so the world uses its own small name lists.
- **World state is a file, not a table.** `world.json` in the content directory keeps the seed and the reference date when neither is configured. Deleting the directory resets the world together with its stored artifacts, so they cannot drift apart. No schema change was needed.
- **Generated once, kept until reset.** Nothing in Pandora expires or rewrites stored content. A full store keeps serving new content but stops writing it.
- **Reserved domain.** The default email domain is on `.example`, so no generated address can belong to a real organization. Set `World:Domain` for realism inside a range.
- **Non-adversarial defaults.** Incident reports describe outages and failures, not intrusions.
- **No new runtime dependencies.** Tests add `Microsoft.AspNetCore.Mvc.Testing` only.
- **Theme markup untouched.** GHOSTS clients select elements by exact class (`SocialHelper.cs`).

### Verification

- `dotnet test src/Ghosts.Pandora.Tests`: 72 passed, 4 skipped (Skia). With `GHOSTS_SKIA_AVAILABLE=1 LD_PRELOAD=/lib/aarch64-linux-gnu/libfreetype.so.6`: 76 passed.
- `dotnet build src/apphost/Ghosts-AppHost.csproj -o /tmp/apphost-verify`: succeeded. The new Aspire resources were not started here.
- Covered by tests:
  - A: a DOCX validates, is contextual, has the right headers and is byte-stable.
  - B: post, comment and like persist.
  - D: a crawl of four website types finds no broken links, and pages are stable.
  - E: the directory, document authors and budget figures agree across formats.
  - F: bad themes and encoded traversal fail safely.
  - G: route compatibility.
  - Unit tests for path interpretation, the store's path safety and concurrency, and HTML encoding.
- Manual: ran the server with SQLite in social and website modes, read generated DOCX, CSV, JSON and PDF text and website articles, and confirmed generated images are byte-stable.

## Increment 2 (2026-11-19): realistic sites

| Change | Main files |
|--------|------------|
| Each social theme was rebuilt to look like the current site: Facebook, Instagram, X, Reddit, LinkedIn, YouTube and Discord. That covers the feed, post, profile, messages, conversation and search pages, mobile layouts, and inline SVG icons with no CDNs. Every GHOSTS client XPath still matches, and theme detection (the first stylesheet and `body[data-theme]`) is kept. Each theme overrides `_postcard`/`_createpost` in its own folder, so `Views/Shared` is unchanged. | `Views/Themes/<theme>/*`, `wwwroot/themes/<theme>/*`, the per-theme entries of `getPostTemplate` in `wwwroot/js/social.js` |
| Added `SocialPresentation` (`@inject … Present` in every view). It supplies display names and headlines from the world, a title and community per post, a stable stock photo, engagement counts that grow over a post's first six hours, relative times and trending topics. | `Infrastructure/SocialPresentation.cs`, `Views/_ViewImports.cshtml` |
| Website mode was redesigned per type: a news masthead and rails; sports with a ticker, recaps and standings; entertainment; and a shopping store with product pages, a cart and search. Unknown paths now render a page derived from the path instead of a 404. | `Services/WebsiteGenerationService.cs` |
| Each site keeps its own theme cookie: `theme-{port}` when the host carries a port, plain `theme` otherwise. Browsers share cookies across the ports of `localhost`, so under Aspire one site's theme took over the others. | `ThemeViewLocationExpander.cs`, `BaseController.cs` |
| The devcontainer forwards Pandora's Aspire ports 6360–6371, so a browser on the host reaches them without manual forwarding. | `.devcontainer/devcontainer.json` |
| Fewer 404s. `/u/{name}` creates the user, follow lookups create users, and a missing avatar falls back to a stock one. | `UsersController.cs`, `Api/RelationshipsController.cs` |
| Fixes. A repeat like no longer returns a 500. The like handler is no longer bound twice after a live post. Live posts only reach their own theme. Typed comments and connection bios are escaped. The Follow/Unfollow toggle URL is correct. Search suggestions come from people on the page. | `PostService.cs`, `social.js`, `connections.js`, `HomeController.cs` |

Verified: `dotnet test` gives 73 passed and 4 skipped (Skia). A browser check of all seven themes, as a fresh visitor, found every client XPath matching, correct theme detection, and no page errors or 4xx responses on the feed, post, profile, messages and search pages.

Known gaps: generated `.mp4` files are placeholder bytes, so the YouTube player can't actually play. Discord channels other than #general show empty, and posts always go to #general. The Discord search page was not restyled. The `default` theme (Pandora's own look) was left as it was. The X client's `findUserName` reads "avatar" from the avatar URL; that is a client bug and was not changed.

### Not done in this increment (next priorities, by impact)

1. **Social identities ↔ world people ↔ GHOSTS NPCs.** Social users are still independent of the world roster. Next: seed or match social accounts to `World.People`, or to GHOSTS NPCs via the API when `GHOSTS_API_URL` is set, so one person is consistent across platforms.
2. **Scenario events.** There is no explicit event or timeline model yet, so an outage cannot be told consistently across a post, a news story and an incident report. The world's systems are the hook for this.
3. **Platform fidelity.** Discord channels, Reddit voting, X reposts, editing and deleting, and notifications. The client-side `sendMessage` in `social.js` is still a placeholder (audit #13).
4. **SharePoint-style repository.** There is no browsable library of the stored documents yet. The store layout (`<format>/<path>`) is ready for one.
5. **Website mode and generated `.html` paths.** These still return the old Lorem page rather than a page of the site.
6. **LLM enrichment.** The `Ollama*` settings remain unused. Any LLM path must stay optional, time-boxed and validated.
7. **SignalR multi-client test** (scenario C). It is not automated yet.
8. Mentioned in the audit and left alone: the cleanup job deletes backdated posts (documented), the `/api/admin/delete` no-op, and `Console.WriteLine` in the view expander.
