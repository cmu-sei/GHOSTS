# Quick Start

## Overview

This guide provides a straightforward installation process using precompiled binaries for both the GHOSTS API server and client. No compilation required!

**New to GHOSTS?** If you want to quickly see NPCs in action, jump to the [GHOSTS Clients](#ghosts-clients) section. For a complete production setup, we recommend installing the API server first, then connecting clients to it.

## :material-api: GHOSTS API Server

The GHOSTS API server consists of four Docker containers:

- **API** - Main control and orchestration server
- **UI** - Web interface for managing machines, groups, and timelines
- **Postgres** - Database for storing all GHOSTS data
- **Grafana** - Visualization dashboard for monitoring NPC activities

### Installation Steps

**1. Install Docker and Docker Compose**

   - Install [Docker](https://docs.docker.com/install/)
   - Install [Docker Compose](https://docs.docker.com/compose/install/)
   - Verify installation: `docker-compose --version`

**2. Download and Run GHOSTS API Containers**

   ```bash
   mkdir ghosts-api
   cd ghosts-api
   curl -O https://raw.githubusercontent.com/cmu-sei/GHOSTS/master/src/Ghosts.Api/docker-compose.yml
   docker-compose up -d
   ```

**3. Verify the Installation**

   - **API**: Open [http://localhost:5000/](http://localhost:5000/) - you should see the API version and test machine entries
   - **UI**: Open [http://localhost:8080/](http://localhost:8080/) - you should see the GHOSTS management interface
   - **Grafana**: Open [http://localhost:3000/](http://localhost:3000/) - you should see the monitoring dashboard

   If any component fails to load, consult the [API troubleshooting section](core/api.md#troubleshooting).

**4. Verify All Containers Are Running**

   ```bash
   docker ps -a
   ```

   You should see four containers: `ghosts-api`, `ghosts-ui`, `ghosts-postgres`, and `ghosts-grafana`.

## :material-account: GHOSTS Clients

GHOSTS clients simulate realistic user activities on workstations. Choose between the full client (for complete simulation with real applications) or GHOSTS Lite (for lightweight network activity simulation).

### Prerequisites for All Clients

**Important:** Clients should be run as a regular user account, not as administrator or root, to accurately simulate realistic user behavior.

**For Browser Automation:** If your timeline includes web browsing, download the appropriate driver and place it in the same folder as the GHOSTS binary:

- **Firefox**: [Download Geckodriver](https://github.com/mozilla/geckodriver/releases)
- **Chrome**: [Download Chromedriver](https://chromedriver.chromium.org/downloads)

**For Email Automation:** See the [Outlook handler documentation](core/handlers/outlook.md) for additional setup requirements.

**For RDP Automation:** See the [RDP handler documentation](core/handlers/rdp.md) for AutoIt DLL registration requirements.

### :material-microsoft-windows: Windows Client

**Requirements:**
- [Microsoft .NET Framework 4.6.1 runtime or later](https://dotnet.microsoft.com/download/dotnet-framework/net47) (SDK not required)

**Installation:**

1. [Download the latest Windows client](https://github.com/cmu-sei/GHOSTS/releases/latest)
2. Extract to a directory (e.g., `c:\exercise\ghosts`)
3. Edit `config/application.json` or `config/application.yaml`:
   ```json
   {
     "ApiRootUrl": "http://YOUR-API-SERVER:5000/api"
   }
   ```
4. Run `ghosts.exe` to start the client

### :material-linux: Linux Client

**Requirements:**

- [.NET 8.0 runtime](https://dotnet.microsoft.com/download) (SDK not required)
- For Ubuntu 24.04, we recommend the [snap installation](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-install?pivots=os-linux-ubuntu-2404&tabs=dotnet8)

**Installation:**

1. [Download the latest Linux client](https://github.com/cmu-sei/GHOSTS/releases/latest)
2. Extract to a directory (e.g., `~/ghosts`)
3. Edit `config/application.yaml`:
   ```yaml
   ApiRootUrl: http://YOUR-API-SERVER:5000/api
   ```
4. Run: `dotnet ghosts.client.linux.dll`

**Note:** Running as root may cause display issues with browser drivers.

For running GHOSTS as a Linux service, see the [Linux service configuration documentation](core/client.md#linux-service-configuration).

### GHOSTS Lite (Lightweight Alternative)

GHOSTS Lite generates network activity and file operations without launching actual applications, ideal for resource-constrained environments.

- [GHOSTS Lite for Windows](https://cmu.box.com/s/2nu9fvzkpp4ku7o2d4uk82lozpkacatn)
- [GHOSTS Lite for Linux](https://cmu.box.com/s/1dy5ip3e3gm1pdo6v9dy21hd4ybe3pa2)

Learn more in the [GHOSTS Lite documentation](core/lite.md).
