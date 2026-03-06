<div>

# GHOSTS NPC Framework

**Realistic User Behavior Modeling and Simulation for Cyber/Cognitive Training, Exercises, and Research**

[![Version](https://img.shields.io/badge/version-9.0-green.svg)](https://github.com/cmu-sei/GHOSTS/releases)
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-20-red.svg)](https://angular.dev/)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](https://github.com/cmu-sei/GHOSTS/pulls)

[Quick Start](https://cmu-sei.github.io/GHOSTS/quickstart/) • [Documentation](https://cmu-sei.github.io/GHOSTS/) • [Issues](https://github.com/cmu-sei/GHOSTS/issues) • [Demo Video](https://www.youtube.com/watch?v=EkwK-cqwjjA)

</div>

[GHOSTS 9 is released, but *still* in active development — see announcement here](https://github.com/cmu-sei/GHOSTS/discussions/582).

---

## Overview

GHOSTS is an NPC (or agent) orchestration framework that models and simulates realistic users on all types of computer systems, generating human-like activity across applications, networks, and workflows. Beyond simple automation, it can dynamically reason, chat, and create content via integrated LLMs, enabling adaptive, context-aware behavior. Designed for cyber training, research, and simulation, it produces realistic network traffic, supports complex multi-agent scenarios, and leaves behind realistic artifacts. Its modular architecture allows the addition of new agents, behaviors, and lightweight clients, making it a flexible platform for high-fidelity simulations.

**Watch a quick demo:** [3-minute introduction on YouTube](https://www.youtube.com/watch?v=EkwK-cqwjjA)

---

## Quick Start

```bash
mkdir ghosts && cd ghosts
curl -O https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml
docker compose up -d
```

| Service | URL | Notes |
|---------|-----|-------|
| GHOSTS Frontend | http://localhost:4200 | Angular management UI |
| GHOSTS API | http://localhost:5000 | REST API + Swagger at `/swagger` |
| Grafana | http://localhost:3000 | Activity dashboards |
| n8n | http://localhost:5678 | Workflow automation |
| PostgreSQL | localhost:5432 | Database (`ghosts`/`scotty@1`) |

Then [install a client](https://cmu-sei.github.io/GHOSTS/quickstart/#ghosts-clients) on each machine you want to simulate and point it at `http://YOUR-API-HOST:5000/api`.

For full setup instructions see the [Quick Start Guide](https://cmu-sei.github.io/GHOSTS/quickstart/).

---

## Architecture

### Core Components

| Component | Tech | Description |
|-----------|------|-------------|
| **Ghosts.Api** | .NET 10 | Central command-and-control server. REST API, SignalR WebSocket hub, NPC orchestration, scenario management. |
| **Ghosts.Frontend** | Angular 20 | Web UI for managing machines, groups, timelines, NPCs, scenarios, and workflow automation. |
| **Ghosts.Client.Windows** | .NET Framework 4.6.2 | Full-featured Windows client with Office automation, browser control, SSH, RDP, and 27+ activity handlers. |
| **Ghosts.Client.Universal** | .NET 9 | Cross-platform client for Linux/Windows with 38+ handlers. |
| **Ghosts.Client.Lite** | .NET 8 | Lightweight client for resource-constrained environments. |
| **Ghosts.Domain** | .NET Standard 2.0 | Shared library: timeline models, handler base classes, client configuration. |

### Supporting Services

| Service | Tech | Description |
|---------|------|-------------|
| **Ghosts.Animator** | .NET Standard 2.0 | NPC persona generation engine (names, careers, social networks, beliefs). |
| **Ghosts.Pandora** | .NET 10 | Dynamic content generation server for realistic web content, blog posts, and documents. |
| **n8n** | Docker | Workflow automation platform. GHOSTS API can schedule and trigger n8n workflows via webhook. |
| **Grafana** | Docker | Real-time dashboards for NPC activity, health metrics, and timeline execution. |

### Infrastructure

| Component | Description |
|-----------|-------------|
| **apphost** | .NET Aspire application host for local development orchestration. |
| **PostgreSQL 16** | Primary data store for all machines, timelines, NPCs, activities, and survey data. |

---

## Repository Layout

```
src/
├── Ghosts.Api/            # .NET 10 API server + docker-compose.yml
├── Ghosts.Domain/         # Shared domain library (netstandard2.0)
├── Ghosts.Frontend/       # Angular 20 management UI
├── Ghosts.Client.Windows/ # .NET 4.6.2 Windows client
├── Ghosts.Client.Universal/ # .NET 9 cross-platform client
├── Ghosts.Client.Lite/    # .NET 8 lightweight client
├── Ghosts.Animator/       # NPC persona generation
├── Ghosts.Pandora/        # Content generation server
├── apphost/               # .NET Aspire host
└── tools/                 # Utilities (load tester, machine adder, email generator)
scripts/
├── build_windows.ps1      # Build Windows client
├── build_universal.py     # Build Universal client
└── horde/                 # Bulk operation scripts
docs/                      # MkDocs documentation source
```

---

## What's New in Version 9

### 🎨 Complete UI Rewrite: Angular 20 Frontend

Replaced the Next.js interface with a modern, full-featured Angular 20 management application:

- **Operations Dashboard** — Real-time overview of connected machines, active NPC animations, and recent timeline executions
- **Belief Explorer** — Visualize and track NPC cognitive state evolution over simulation steps
- **NPC Action Menu** — Enhanced NPC management with improved controls for persona generation and social graph visualization
- **Workflow Scheduler** — Schedule and monitor n8n workflows with live execution logs and real-time status updates
- **Enhanced Timeline Management** — Streamlined creation, assignment, and deployment of timelines to machines and groups
- **Scenario Management** — Define, execute, and track training scenarios with improved planning and execution tracking

### ⚡ Platform Modernization

- **.NET 10 Upgrade** — API (`Ghosts.Api`) and Pandora content server migrated to .NET 10 with latest dependency updates
- **.NET Aspire Host** — Optional application host for local development with integrated service discovery and health dashboards
- **Grafana Provisioning** — Pre-configured dashboards for cognitive range monitoring, NPC activity, and belief systems

### 🤖 Enhanced Cognitive Capabilities

- **Belief System** — NPCs now maintain belief states that evolve over time based on interactions and experiences
- **Knowledge Graphs** — Track NPC learning and knowledge acquisition with visualization tools
- **Social Connections API** — New endpoints for managing NPC relationships, preferences, and social networks
- **Improved Social Graph** — Enhanced visualization and management of NPC social networks and connections

### 🔄 Workflow Automation

- **n8n Integration** — Native workflow automation platform integration with:
  - Cron-scheduled workflow execution
  - Real-time monitoring with HTTP status and response preview
  - Base workflow templates included
  - Environment variable configuration for API URL and authentication

### 🐛 Bug Fixes & Improvements

- Fixed trackables history endpoint (#581)
- Improved username generation (proper formatting, removes spaces)
- Resolved SVG asset build issues
- Animation cancellation token improvements
- NPCs can now be tied to specific scenarios
- Timeline delivery improvements (by machine and by group)

<details>
<summary>Version 8.x history</summary>

### Version 8.2
- GHOSTS UI (Next.js) — now replaced by the Angular 20 frontend in v9.
- GHOSTS Lite beta release.
- LLM/Shadows integration — superseded by [RangerAI](https://github.com/cmu-sei/rangerai).
- Bug fixes: default GUID (#385), client path (#384), animation cancellation token.

### Version 8.1
- GHOSTS Lite beta.
- API cleanup for machine updates and groups.
- Simplified JSON payloads.
- Timeline delivery by machine and by group.

### Version 8.0 (breaking — fresh install required)
- ANIMATOR and SPECTRE merged into core; both archived.
- Migrated from MongoDB to PostgreSQL.
- WebSocket (SignalR) support; clients are now always-connected.
- Single `docker-compose.yml` for the full stack.
- API endpoints reorganized.
- Random delay support in timelines.

</details>

---

## Use Cases

- **Cyber Training & Exercises** — Populate training environments with realistic user activity.
- **Red Team Operations** — Generate believable background noise during security assessments.
- **Blue Team Training** — Create realistic network traffic for detection and analysis practice.
- **Research & Development** — Test security tools and detection algorithms with realistic data.
- **Cognitive Range Development** — Build immersive environments with autonomous NPCs that reason and interact.

---

## Contributing

1. Report bugs and request features via the [GitHub issue tracker](https://github.com/cmu-sei/GHOSTS/issues).
2. Fork, create a feature branch, and submit a pull request.
3. Documentation improvements are always welcome.

---

## Related Projects

- **[RangerAI](https://github.com/cmu-sei/rangerai)** — AI/LLM integration layer for GHOSTS (successor to Shadows).

---

## License

MIT License. See [LICENSE.md](LICENSE.md) for full details.

**Distribution Statement**: [DISTRIBUTION STATEMENT A] This material has been approved for public release and unlimited distribution.

Copyright 2017–2026 Carnegie Mellon University. All Rights Reserved.

---

<div>

[Docs](https://cmu-sei.github.io/GHOSTS/) • [GitHub](https://github.com/cmu-sei/GHOSTS) • [Issues](https://github.com/cmu-sei/GHOSTS/issues) • [Releases](https://github.com/cmu-sei/GHOSTS/releases)

</div>
