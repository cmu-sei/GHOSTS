# GHOSTS Frontend

The GHOSTS Frontend is an Angular 20 single-page application that provides a management interface for the GHOSTS framework. It communicates with the GHOSTS API via REST and SignalR WebSocket.

**Docker image:** `dustinupdyke/ghosts-frontend` (port 4200 / 80 inside container)
**Source:** `src/Ghosts.Frontend/`

---

## Installation

The frontend is included in the standard `docker-compose.yml`. No separate setup is required.

To run it standalone:

```yaml
ghosts-frontend:
  image: dustinupdyke/ghosts-frontend
  container_name: ghosts-frontend
  ports:
    - "4200:80"
  environment:
    API_URL: http://YOUR-API-HOST:5000/api
    N8N_API_URL: http://YOUR-N8N-HOST:5678
```

### Local Development

```bash
cd src/Ghosts.Frontend
npm install
npm start        # serves on http://localhost:4200 with hot reload
npm run build    # production build to dist/
npm test         # run unit tests
```

Edit `src/assets/config.json` to point at your API:

```json
{
  "apiUrl": "http://localhost:5000/api",
  "n8nApiUrl": "http://localhost:5678"
}
```

---

## Features

### Machines

View all registered GHOSTS client machines. From the machine list you can:

- Search by name, ID, or status
- View a machine's full activity history
- Push a new timeline update to a specific machine
- Add machines to groups
- View the machine's current timeline and survey data

### Machine Groups

Organize machines into groups for bulk operations:

- Create, rename, and delete groups
- Add or remove machines from a group
- Deploy a timeline to all machines in a group simultaneously

### Timelines

Browse and manage timeline templates:

- View all timeline handler definitions
- Create new timelines or edit existing ones
- Assign a timeline to a specific machine or group

See [timeline configuration](api/timelines.md) for the timeline JSON format.

### NPCs

Manage generated NPC personas:

- Browse all NPCs with their profile attributes (name, rank, unit, career, etc.)
- View an NPC's social connections and relationships
- Generate new NPCs using the Animator (configurable count and parameters)
- View and assign NPC actions via the NPC action menu

### Scenarios

Plan and track training/exercise scenarios:

- Create scenarios with associated parameters and injects
- Run scenarios and monitor execution events in real time
- Review execution history and metric snapshots

### Animations

Start and stop the server-side animation jobs that drive autonomous NPC behavior:

| Job | Description |
|-----|-------------|
| Social Graph | Evolves NPC relationship networks |
| Social Sharing | NPCs share content across a social platform |
| Social Belief | NPC beliefs evolve based on interactions |
| Chat | NPCs engage in LLM-powered conversations |
| Full Autonomy | NPCs make fully autonomous decisions |

Each job can be started with custom configuration and stopped independently.

### Operations Dashboard

Real-time overview showing:

- Connected machine count and status
- Currently running animation jobs
- Recent timeline execution activity

### Belief Explorer

Inspect individual NPC belief states:

- Browse beliefs by NPC
- View belief values and how they have changed over simulation steps
- Filter by topic or time range

### RangerAI / n8n Workflow Scheduling

Schedule n8n webhook-triggered workflows on a cron schedule:

- Browse active n8n workflows fetched from the n8n API
- Select a webhook workflow and set a cron schedule (5- or 6-field)
- The GHOSTS API calls the workflow's current webhook URL at each scheduled time
- A live execution log (via SignalR) shows each result: timestamp, workflow name, HTTP status, and any error detail
- Stop a running schedule at any time

**Prerequisites:** Set `N8N_API_URL` and `N8N_API_KEY` environment variables on the GHOSTS API container. Workflows must be **Active** in n8n (the toggle in the n8n workflow editor) before scheduling.

---

## Real-Time Updates

The frontend maintains a SignalR WebSocket connection to `/api/hubs/activities` for:

- Live NPC animation events (social graph updates, belief changes, chat messages)
- Workflow execution results from the scheduler
- Machine connectivity events

---

## Architecture Notes

- **Standalone Angular components** throughout — no NgModules.
- **Signals** (`signal()`, `computed()`) used for reactive state; compatible with `ChangeDetectionStrategy.OnPush`.
- **Material Design** components via `@angular/material`.
- Runtime configuration is loaded from `/assets/config.json` before the application bootstraps, making it possible to inject environment-specific URLs into the Docker image at startup via the `docker-entrypoint.sh` script.
