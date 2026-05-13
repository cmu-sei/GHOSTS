# Quick Start

## Overview

This guide covers deploying the GHOSTS API stack and connecting clients to it. The API and frontend images are built locally from source using Docker — no manual compilation or SDK installation is required beyond Docker itself.

**New to GHOSTS?** Start with the API stack first, then connect one or more clients.

???+ info "Stability"
    GHOSTS 9 is released and actively used in production exercises. The **API**, **domain models**, **Docker Compose stack**, and **Windows/Universal clients** are stable. Newer features — **Scenario Builder**, **n8n integration**, and **Belief Engine** — are under active development and may change between minor releases.

---

## :material-api: GHOSTS API Stack

The GHOSTS API stack runs as five Docker containers:

| Container | Image | Port | Purpose |
|-----------|-------|------|---------|
| `ghosts-frontend` | *(built from source)* | 4200 | Angular 20 management UI |
| `ghosts-api` | *(built from source)* | 5000 | REST API + SignalR WebSocket |
| `ghosts-postgres` | `postgres:16.8` | 5432 | PostgreSQL database |
| `ghosts-n8n` | `docker.n8n.io/n8nio/n8n` | 5678 | Workflow automation |
| `ghosts-grafana` | `grafana/grafana` | 3000 | Activity dashboards |

### Prerequisites

- [Docker](https://docs.docker.com/install/) and [Docker Compose](https://docs.docker.com/compose/install/) (v2+)
- Minimum 4 GB RAM (8 GB+ recommended for larger deployments)
- 20 GB disk space
- Ports 3000, 4200, 5000, 5432, and 5678 available on the host

### Installation

**1. Clone the repository**

The Docker Compose file builds the API and frontend images from source and mounts configuration files (Grafana dashboards, n8n workflows) from the repo, so you need the full repository:

```bash
git clone https://github.com/cmu-sei/GHOSTS.git
cd GHOSTS/src/Ghosts.Api
```

**2. Build and start all containers**

```bash
docker compose up -d --build
```

The first run builds the API and frontend images locally — this may take a few minutes depending on your connection and hardware. Subsequent runs reuse cached layers.

???+ tip "GitHub Codespaces / proxy environments"
    If the build fails with SSL certificate errors (e.g., `PartialChain`), your environment uses a TLS-intercepting proxy whose CA is not trusted inside the build container. Fix this by building with host networking:

    ```bash
    docker compose build --network=host
    docker compose up -d
    ```

**3. Verify the installation**

```bash
docker ps -a
```

You should see all five containers in a running state.

| Component | URL | Expected result |
|-----------|-----|-----------------|
| Frontend | http://localhost:4200 | Angular management UI |
| API | http://localhost:5000 | API home page |
| API Swagger | http://localhost:5000/swagger | Interactive API docs |
| Grafana | http://localhost:3000 | Monitoring dashboard (default login: `admin`/`admin`) |
| n8n | http://localhost:5678 | Workflow automation UI |

If the API shows a database error, wait 15–30 seconds for PostgreSQL to finish initializing, then refresh.

### Environment Variables

The compose file uses these defaults, which can be overridden with a `.env` file in the same directory:

| Variable | Default | Description |
|----------|---------|-------------|
| `WEB_API_URL` | `http://localhost:5000/api` | API URL used by the frontend (must be reachable by the browser) |
| `WEB_N8N_API_URL` | `http://localhost:5678` | n8n URL used by the frontend (must be reachable by the browser) |
| `N8N_API_URL` | `http://ghosts-n8n:5678/api/v1/workflows` | n8n REST API URL used by the API container (inter-container) |
| `N8N_API_KEY` | *(empty)* | n8n API key — **required** for workflow scheduling. Generate one in n8n: Settings > API > Create API Key. |
| `POSTGRES_PASSWORD` | `Scotty@@1!` | PostgreSQL password. **Change this** for any non-local deployment. |

### Managing the Stack

```bash
# Stop all containers
docker compose down

# Pull latest source and rebuild
git pull && docker compose up -d --build

# View logs for the API
docker compose logs -f ghosts-api

# Restart a single service
docker compose restart ghosts-api
```

### Data Persistence

Data is stored in named Docker volumes:

- `ghosts-postgres-data` — PostgreSQL data
- `ghosts-grafana-data` — Grafana configuration and dashboards
- `ghosts-n8n-data` — n8n workflow definitions and credentials

These persist across `docker compose down` / `up` cycles. To reset the database:

```bash
docker compose down -v   # removes all named volumes
docker compose up -d --build
```

---

## :material-account: GHOSTS Clients

GHOSTS clients simulate realistic user activity on target machines. Install one client per machine you want to simulate.

### Which Client Should I Use?

| Client | Runtime | OS | Best For |
|--------|---------|----|----------|
| **Windows** | [.NET Framework 4.6.1+](https://go.microsoft.com/fwlink/?LinkId=2099467) | Windows 7/10/11, Server 2012+ | Full Office/browser/RDP automation. Use when participants observe the desktop. |
| **Universal** | [.NET 9](https://dotnet.microsoft.com/download/dotnet/9.0) | Windows, Linux, macOS | Cross-platform, 38+ handlers. **Default choice** for Linux or mixed environments. |
| **Lite** | [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) | Windows, Linux | Lightweight traffic generation without real apps. Run 100-500+ NPCs per host. |

> **Not sure?** Start with **Universal**. Switch to **Windows** only if you need Office automation, or to **Lite** if you need high NPC density on limited hardware.

### Prerequisites

Run clients as a **regular user account**, not as administrator or root, to produce realistic behavior and avoid permission issues with browser drivers.

For browser automation, place the appropriate WebDriver binary in the same directory as the GHOSTS client binary:

- **Firefox**: [Geckodriver](https://github.com/mozilla/geckodriver/releases)
- **Chrome/Chromium**: [ChromeDriver](https://chromedriver.chromium.org/downloads)

### :material-microsoft-windows: Windows Client

**Requirements:** [.NET Framework 4.6.1 runtime](https://go.microsoft.com/fwlink/?LinkId=2099467) or later (runtime only, SDK not required).

1. [Download the latest Windows client](https://github.com/cmu-sei/GHOSTS/releases/latest)
2. Extract to a directory (e.g., `C:\ghosts`)
3. Edit `config/application.json`:
   ```json
   {
     "ApiRootUrl": "http://YOUR-API-HOST:5000/api"
   }
   ```
4. Run `ghosts.exe`

### :material-linux: Linux / Universal Client

**Requirements:** [.NET 9 runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (runtime only, SDK not required).

For Ubuntu 24.04, use the [snap installation](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?pivots=os-linux-ubuntu-2404&tabs=dotnet8).

1. [Download the latest Universal client](https://github.com/cmu-sei/GHOSTS/releases/latest)
2. Extract to a directory (e.g., `~/ghosts`)
3. Edit `config/application.yaml`:
   ```yaml
   ApiRootUrl: http://YOUR-API-HOST:5000/api
   ```
4. Run:
   ```bash
   dotnet ghosts.client.universal.dll
   ```

**Note:** Do not run as root — browser drivers may fail to launch in headless mode under root.

To run as a Linux systemd service, see the [client documentation](core/client.md#linux-service-configuration).

### GHOSTS Lite (Lightweight Client)

GHOSTS Lite generates network and file activity without launching real applications. Use it for resource-constrained machines or when participants will not directly interact with the machine.

**Requirements:** [.NET 8 runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

1. [Download the latest Lite client](https://github.com/cmu-sei/GHOSTS/releases/latest)
2. Edit `config/application.yaml` with your API URL
3. Run:
   ```bash
   dotnet ghosts.client.lite.dll
   ```

See the [GHOSTS Lite documentation](core/lite.md) for configuration details.

---

## Next Steps

- Use the [Frontend](http://localhost:4200) to create machine groups, assign timelines, and monitor activity
- Browse the [API via Swagger](http://localhost:5000/swagger) to explore all endpoints
- Generate NPCs using the [Animator](animator/index.md)
- Configure [Grafana dashboards](core/grafana.md) for monitoring
- Set up [n8n workflows](new.md#n8n-workflow-integration) for RangerAI automation
