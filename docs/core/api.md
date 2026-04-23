# GHOSTS API

The GHOSTS API is the central command-and-control server for the GHOSTS framework. It manages clients, distributes timelines, records activity, stores NPC data, and exposes a REST API plus a SignalR WebSocket hub.

---

## Stack Overview

The API stack runs as five Docker containers:

| Container | Image | Port | Purpose |
|-----------|-------|------|---------|
| `ghosts-api` | `dustinupdyke/ghosts` | 5000 | .NET 10 REST API + SignalR |
| `ghosts-frontend` | `dustinupdyke/ghosts-frontend` | 4200 | Angular 20 management UI |
| `ghosts-postgres` | `postgres:16.8` | 5432 | PostgreSQL 16 database |
| `ghosts-n8n` | `docker.n8n.io/n8nio/n8n` | 5678 | n8n workflow automation |
| `ghosts-grafana` | `grafana/grafana` | 3000 | Activity dashboards |

---

## Installation

### Step 1 — Choose a Host

- **Development / testing**: your local machine
- **Training exercises**: a dedicated server, VM, or cloud instance
- **Production**: a container orchestration platform (AWS ECS, Kubernetes, Docker Swarm)

**Minimum requirements:**

- Docker and Docker Compose v2+
- 4 GB RAM (8 GB+ recommended)
- 20 GB disk space
- Ports 3000, 4200, 5000, 5432, and 5678 available

### Step 2 — Deploy

```bash
mkdir ghosts && cd ghosts
curl -O https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml
docker compose up -d
```

Verify:

```bash
docker ps -a
```

All five containers should be in a running state. PostgreSQL needs ~15 seconds on first start; the API will retry the connection automatically.

### Step 3 — Verify

| URL | Expected |
|-----|----------|
| http://localhost:5000 | API home page |
| http://localhost:5000/swagger | Swagger UI (API v9) |
| http://localhost:4200 | Angular management frontend |
| http://localhost:3000 | Grafana (default: `admin`/`admin`) |
| http://localhost:5678 | n8n workflow editor |

---

## Configuration

### Environment Variables

Set these in a `.env` file alongside `docker-compose.yml` or directly in your container environment:

| Variable | Default | Description |
|----------|---------|-------------|
| `N8N_API_URL` | `http://host.docker.internal:5678/api/v1/workflows` | n8n REST API endpoint (used by the API to list and validate workflows) |
| `N8N_API_KEY` | *(empty)* | n8n API key — **required** for workflow scheduling. Generate in n8n: Settings > API > Create API Key. |
| `WEB_API_URL` | `http://host.docker.internal:5000/api` | GHOSTS API URL served to the frontend container |
| `WEB_N8N_API_URL` | `http://host.docker.internal:5678` | n8n base URL served to the frontend container |
| `POSTGRES_PASSWORD` | `scotty@1` | PostgreSQL password. **Change this** for any non-local deployment. |

### appsettings.json (Advanced)

Key settings in `Ghosts.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=ghosts-postgres;Port=5432;Database=ghosts;User Id=ghosts;Password=scotty@1;"
  },
  "ApplicationSettings": {
    "OfflineAfterMinutes": 30,
    "MatchMachinesBy": "name",
    "AnimatorSettings": {
      "Animations": {
        "IsEnabled": true
      }
    }
  },
  "CorsPolicy": {
    "Origins": ["http://localhost:4200"]
  }
}
```

Override values for production via environment variables or a mounted `appsettings.Production.json`.

---

## Managing the Stack

```bash
# Stop
docker compose down

# Update to latest images
docker compose pull && docker compose up -d

# Restart a specific service
docker compose restart ghosts-api

# Follow API logs
docker compose logs -f ghosts-api
```

### Data Persistence

| Directory | Contents |
|-----------|----------|
| `./_db` | PostgreSQL data (machines, NPCs, timelines, activities) |
| `./_g` | Grafana dashboards and configuration |
| `./n8n_data` | n8n workflows and credentials |

**Reset the database (destroys all data):**

```bash
docker compose down && rm -rf ./_db && docker compose up -d
```

---

## API Reference

Interactive documentation is available at `http://YOUR-API-HOST:5000/swagger` when the API is running.

### Client Endpoints

Used by deployed GHOSTS client agents:

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/clientid` | Machine self-registration |
| GET/POST | `/api/clientupdates` | Fetch pending timeline updates |
| POST | `/api/clientresults` | Submit activity results |
| POST | `/api/clienttimeline` | Submit active timeline |
| POST | `/api/clientsurvey` | Submit system survey data |

### Management Endpoints

Used by the frontend and operators:

| Resource | Base Path |
|----------|-----------|
| Machines | `/api/machines` |
| Machine Groups | `/api/machinegroups` |
| Timelines | `/api/timelines` |
| Machine Timelines | `/api/machinetimelines` |
| Machine Updates | `/api/machineupdates` |
| NPCs | `/api/npcs` |
| NPC Generation | `/api/npcs/generate` |
| Scenarios | `/api/scenarios` |
| Executions | `/api/executions` |
| Trackables | `/api/trackables` |
| Surveys | `/api/surveys` |
| Webhooks | `/api/webhooks` |
| Animation Jobs | `/api/animations/jobs` |
| n8n Workflows | `/api/animations/workflows` |

### SignalR Hub

Clients connect to `/clientHub` (WebSocket). Hub methods:

| Method | Direction | Purpose |
|--------|-----------|---------|
| `SendId` | Client → Server | Machine identification on connect |
| `SendResults` | Client → Server | Timeline execution results |
| `SendSurvey` | Client → Server | System survey data |
| `SendHeartbeat` | Client → Server | Keep-alive |
| `ReceiveId` | Server → Client | Returns machine GUID after registration |

The frontend connects to `/api/hubs/activities` for real-time animation and workflow events.

---

## Troubleshooting

### API returns database error on startup

PostgreSQL takes ~15 seconds to initialize on first run. The API retries automatically — wait and refresh.

If it persists, check PostgreSQL logs:

```bash
docker compose logs ghosts-postgres
```

### Cannot connect from a client to the API

1. Confirm the API is reachable from the client machine:
   ```bash
   curl http://YOUR-API-HOST:5000/api
   ```
2. Ensure port 5000 is open in the firewall on the API host.
3. Check `config/application.json` (or `application.yaml`) on the client has the correct `ApiRootUrl`.

### Grafana container keeps restarting

Check ownership of the `_g` directory:

```bash
sudo chown -R 472:472 ./_g
docker compose restart ghosts-grafana
```

### Social graph page shows no data

This is expected on fresh installations. Social graphs are created when NPCs are generated and animations are run. See [Animator documentation](../animator/index.md).

---

## Next Steps

- Connect [client machines](client.md)
- Explore the [Frontend](ui.md) for machine and timeline management
- Configure [Grafana dashboards](grafana.md)
- Learn about [timeline configuration](api/timelines.md)
- Generate NPCs with the [Animator](../animator/index.md)
