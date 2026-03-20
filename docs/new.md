# What's New in GHOSTS 9

## Version 9.0

### Angular 20 Frontend

The previous Next.js UI has been replaced by a full Angular 20 management application (`src/Ghosts.Frontend`). It is served at port 4200 (or port 80 inside the Docker container) and covers:

- **Machines** — register, search, group, view activity history, push timeline updates
- **Machine Groups** — create and manage groups; deploy timelines to all members at once
- **Timelines** — browse, create, and assign timeline templates
- **NPCs** — view and generate NPC personas; browse belief state and social connections
- **Scenarios** — define, execute, and track training scenarios
- **Animations** — start and stop Social Graph, Social Sharing, Social Belief, Chat, and Full Autonomy animation jobs
- **Operations Dashboard** — real-time overview of NPC activity and system health
- **Belief Explorer** — visualize NPC cognitive state evolution over time
- **RangerAI / n8n Workflows** — schedule and monitor n8n webhook-triggered workflows with a live execution log

Configuration is provided via `src/Ghosts.Frontend/src/assets/config.json`:

```json
{
  "apiUrl": "http://localhost:5000/api",
  "n8nApiUrl": "http://localhost:5678"
}
```

### .NET 10

The API (`Ghosts.Api`) and content server (`Ghosts.Pandora`) are now built on .NET 10.

### n8n Workflow Integration

GHOSTS can schedule n8n workflows on a cron schedule and call their production webhook URLs. Each execution result (success or failure with HTTP status and response preview) is surfaced in real time via the Angular frontend's live execution log.

Environment variables required on the API container:

| Variable | Example | Purpose |
|----------|---------|---------|
| `N8N_API_URL` | `http://n8n:5678/api/v1/workflows` | n8n REST API endpoint |
| `N8N_API_KEY` | `eyJ...` | n8n API key |

### Operations Dashboard

New dashboard page in the frontend providing a real-time overview of connected machines, active NPC animations, and recent timeline executions.

### Belief Explorer

New view for inspecting individual NPC belief states, including how beliefs have evolved over simulation steps.

### .NET Aspire Host

`src/apphost` provides an optional .NET Aspire application host that orchestrates the full local development stack (API, Frontend, PostgreSQL, QDRANT, Pandora, n8n, Grafana) with integrated service discovery and health dashboards.

---

## Previous Versions

<details>
<summary>Version 8.2</summary>

- Introduced the Next.js UI (now replaced by the Angular 20 frontend in v9).
- GHOSTS Lite beta release.
- LLM Shadows integration (deprecated — use [RangerAI](https://github.com/cmu-sei/rangerai)).
- Bug fixes: default GUID (#385), client path (#384), animation cancellation token.

</details>

<details>
<summary>Version 8.1</summary>

- GHOSTS Lite beta.
- API cleanup for machine updates and groups.
- Simplified JSON payloads.
- Timeline delivery by machine and by group.

</details>

<details>
<summary>Version 8.0 (breaking — fresh install required)</summary>

- ANIMATOR and SPECTRE merged into core; both projects archived.
- Migrated from MongoDB to PostgreSQL.
- WebSocket (SignalR) support; clients are always-connected.
- Single `docker-compose.yml` for the full stack.
- API endpoints reorganized.
- Random delay support in timelines.
- Grafana container no longer runs as root.

</details>
