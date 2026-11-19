# Pandora Audit (2026-11-19)

Baseline: `dotnet test src/Ghosts.Pandora.Tests` builds and passes 10, skips 4 (Skia tests are opt-in).
Findings below were checked against a running instance (`dotnet run`, SQLite, ports 5310/5311) unless
marked "read".

## Critical

| # | Problem | Evidence | Files | Correction |
|---|---------|----------|-------|------------|
| 1 | Every Word and PowerPoint file is **0 bytes**. | `GET /reports/2026/q3/network-assessment.docx` and `/deck.ppt` return 200 with an empty body. `memoryStream.ToArray()` runs before the OpenXML package is disposed, so nothing has been written yet. | `Infrastructure/Services/OfficeDocumentGenerationService.cs` | Dispose the package before reading the stream, build a PowerPoint package with a theme and layout, and check both with `OpenXmlValidator` in tests. |
| 2 | Stored XSS on every theme. Post and comment text is rendered with `@Html.Raw`, and the n8n workflows post raw LLM output as `message`. | 30 `Html.Raw(...Message)` sites (read). | `Views/**`, `Views/Shared/_PostCard.cshtml`, `_Post.cshtml` | Encode message text and allow only the `<br/><img src="/images/{guid}/image.jpg\|png"/>` markup that Pandora adds for uploads. |
| 3 | Real-time XSS: SignalR pushes the raw message, and `social.js`/`posts.js` put `user` and `message` into `innerHTML` for every connected client. | read | `Hubs/PostsHub.cs`, `HomeController.Post`, `Api/PostsController.Post`, `wwwroot/js/social.js`, `wwwroot/js/posts.js` | Broadcast the encoded message and escape `user` in the client templates. |

## High

| # | Problem | Evidence | Files | Correction |
|---|---------|----------|-------|------------|
| 4 | Generated content is never stored. `StoreResults` and `ContentCacheDirectory` are documented but nothing reads them, because `ContentManager` is registered and never called. The same URL returns a different document on every request, and the website homepage changes on every load. | `cmp` of two `GET /` responses differs. | `Controllers/PandoraController.cs`, `Infrastructure/Services/ContentManager.cs` | Store generated content per URL so it stays stable. |
| 5 | `ContentManager` itself is unsafe: it URL-decodes an already decoded path a second time and `Path.Combine`s it under the cache root, so `..%2F` escapes the root (read). Writes are not atomic, and two concurrent requests both generate. | read | `ContentManager.cs` | Validate segments, check the path stays under the root, write to a temp file then rename, lock per key, cap total size. |
| 6 | Content ignores the request. The title, kind, organization and period are never taken from the path, so `/finance/quarterly/budget.xlsx` returns a contact list and `/employees/directory.json` returns unrelated records. Everything else is Lorem ipsum. | probe | `ContentGenerationHelper.cs`, `*GenerationService.cs` | Interpret the path into a kind, title, period and department, and generate structured documents from one shared synthetic organization. |
| 7 | Website mode has no pages behind its links. Every `/article/{random}`, section, About and Contact link returns 404, and the article IDs are random on each load. | crawl of `/` in website mode: 34 of 35 links are 404 | `HomeController.CatchAll`, `WebsiteGenerationService.cs` | Generate a site whose articles have stable IDs, plus section, detail, About and Contact pages. |
| 8 | An unknown theme gives a 500 (`?theme=bogus` → "view 'Index' was not found"). The theme from the query or cookie also goes into view paths without being checked. | probe | `BaseController.ThemeRead`, `ThemeViewLocationExpander`, `Users/Search/DirectMessagesController` | Accept only themes that exist, and otherwise fall back to the configured default. |
| 9 | Payload mappings can never match. The route is `/payloads/{**path}` but the match is against `"/payloads/" + path`, while the configured URLs (`/downloads/document`, and `/1/` in the docs) never start with `/payloads`. | `GET /downloads/document` → generated HTML. `GET /payloads/downloads/document` → 404. | `Controllers/PayloadController.cs`, `PandoraFileExtensionMiddleware.cs` | Serve a payload at its configured URL, ahead of generation. |

## Medium

| # | Problem | Files | Correction |
|---|---------|-------|------------|
| 10 | `.doc`, `.xls` and `.ppt` return OOXML bytes under the legacy file name. Office shows a format/extension mismatch warning. | `PandoraController.cs` | Keep the honest OOXML MIME type and name the download `.docx`/`.xlsx`/`.pptx`. Pandora cannot write the legacy binary formats. |
| 11 | `ApplicationConfiguration` is bound by its own `ConfigurationBuilder` (`appsettings.json` + environment only), so `appsettings.{Environment}.json`, command-line arguments and host or test configuration are ignored. | `ApplicationConfiguration.cs`, `Program.cs` | Bind from `builder.Configuration`, which includes the same sources and more. |
| 12 | On ARM64 without `LD_PRELOAD=libfreetype.so.6`, the first generated image kills the process (`libSkiaSharp.so: undefined symbol: FT_Get_BDF_Property`). Aspire sets the preload. Standalone `dotnet run` does not. | `apphost/AppHost.cs` | Document it, since this is native packaging rather than Pandora code. |
| 13 | The client-side `sendMessage` in `social.js` logs to the console and shows "Message sent!" without sending anything. | `wwwroot/js/social.js:753` | Next increment, as part of platform fidelity. |
| 14 | `DELETE /api/admin/delete` is documented as "Resets server by deleting all posts" but its body is commented out and it returns 204. | `Controllers/Api/AdminController.cs` | Mention only. This looks deliberate. |
| 15 | Cleanup deletes posts by *post date*, so backdated seed posts older than 60 h disappear within 15 minutes when disk use cannot be read (any non-overlay host). This is documented. | `CleanupService.cs` | Mention only. It is documented, and changing it is a behaviour change. |

## Low / code health (mentioned, not changed)

- `HomeController.CatchAll` calls `RedirectToAction(...)` and throws the result away (dead code).
- The `Ollama*` settings and the `ImageGeneration`/`VideoGeneration`/`VoiceGeneration` flags are read nowhere.
- `PostService.Duplicates` lower-cases inside the query on every post, so no index can be used.
- `ThemeViewLocationExpander` writes `Console.WriteLine` on every view lookup.
- The website homepage links `/css/main.css`, which is the 480 KB social stylesheet.
- `/pandora/about` reports version `1.0.0`, while the product is 9.8.0.
- Database: `EnsureCreated` with no migrations is the GHOSTS-wide design (a new version expects a fresh database), so it is kept. Pandora's model is the same on SQLite and PostgreSQL.

## Compatibility constraints found

- The GHOSTS Universal and Windows clients drive the themes through Selenium using **exact** class
  XPaths (`//button[@class='post-btn']`, `//input[@class='message-input']`, `like-it`, `post-input`, …; see
  `Ghosts.Client.Universal/Handlers/SocialHelper.cs`). Theme markup classes must not change.
- n8n and the API post with a form-encoded `user` + `message` to `/` and `/api/posts`, and send DMs to `/messages/send`.
- NPC activity is reported through `GET /api/npcs/lookup?username=` and `POST /api/npcs/{id}/activity`.
