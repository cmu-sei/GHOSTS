# GHOSTS LITE

GHOSTS LITE is a lightweight version of GHOSTS that generates realistic network activity and file operations without launching actual applications.

## Overview

### What is GHOSTS LITE?

GHOSTS LITE programmatically simulates user activity without the overhead of running real applications. Instead of launching Firefox or Chrome, it generates HTTP requests directly. Instead of launching Word or Excel, it creates files programmatically. This provides the same network-level realism while consuming significantly fewer system resources.

### When to Use GHOSTS LITE

**Use GHOSTS LITE when:**

- Participants won't have direct access to simulated machines
- You need to maximize the number of NPCs on limited hardware
- Network traffic patterns are more important than application-level realism
- Resource constraints (CPU, memory, storage) are a concern

**Use Full GHOSTS when:**

- Participants will interact with or observe the simulated machines directly
- You need complete application-level fidelity (e.g., actual browser windows, Office documents)
- You're testing application-level behaviors or vulnerabilities
- Resources are not constrained

### Key Features

- **Lightweight**: Uses minimal CPU, memory, and storage
- **Realistic Network Traffic**: Generates HTTP/HTTPS requests that appear authentic
- **File Generation**: Creates files without launching Office applications
- **API Integration**: Connects to GHOSTS API for centralized management
- **Timeline Support**: Uses the same timeline format as full GHOSTS

## Resource Comparison

| Metric | Full GHOSTS | GHOSTS LITE |
|--------|-------------|-------------|
| Memory per Client | 500MB - 2GB | 50MB - 200MB |
| CPU Usage | Moderate-High | Low |
| Disk I/O | High | Low |
| Applications Launched | Yes (browsers, Office, etc.) | No |
| Network Traffic | Authentic | Authentic |

## Installation

### Prerequisites

- **.NET 8.0 Runtime** or later ([download here](https://dotnet.microsoft.com/download))
- Operating System: Windows or Linux
- Network access to GHOSTS API server (if using centralized management)

### Download and Installation

**Windows:**

1. [Download GHOSTS LITE for Windows](https://cmu.box.com/s/2nu9fvzkpp4ku7o2d4uk82lozpkacatn)
2. Extract to a directory (e.g., `c:\exercise\ghosts-lite`)
3. Configure the API connection in `config/application.yaml`:
   ```yaml
   ApiRootUrl: http://YOUR-API-SERVER:5000/api
   ```
4. Run from PowerShell or CMD:
   ```bash
   dotnet Ghosts.Client.Lite.dll
   ```

**Linux:**

1. [Download GHOSTS LITE for Linux](https://cmu.box.com/s/1dy5ip3e3gm1pdo6v9dy21hd4ybe3pa2)
2. Extract to a directory (e.g., `~/ghosts-lite`)
3. Configure the API connection in `config/application.yaml`:
   ```yaml
   ApiRootUrl: http://YOUR-API-SERVER:5000/api
   ```
4. Run:
   ```bash
   dotnet Ghosts.Client.Lite.dll
   ```

### Verify Installation

After starting GHOSTS LITE, check the logs to confirm it's running:

```bash
# View the main application log
cat logs/app.log

# Check for successful API connection
tail -f logs/clientupdates.log
```

## Configuration

GHOSTS LITE uses the same configuration file structure as full GHOSTS. The primary difference is that it only supports a subset of handlers optimized for lightweight operation.

### Supported Handlers

GHOSTS LITE supports the following timeline handlers:

- **Web Traffic**: Simulates HTTP/HTTPS requests without launching browsers
- **File Operations**: Creates and modifies files without launching applications
- **Network Shares**: Accesses network resources
- **Basic Commands**: Executes lightweight system commands

### Sample Timeline

Here's a basic timeline configuration for GHOSTS LITE:

```yaml
TimeLineHandlers:
  - HandlerType: LiteHttp
    Initial: ""
    UtcTimeOn: "00:00:00"
    UtcTimeOff: "24:00:00"
    Loop: true
    TimeLineEvents:
      - Command: random
        CommandArgs:
          - "https://www.example.com"
          - "https://www.google.com"
          - "https://www.github.com"
        DelayAfter: 30000
        DelayBefore: 5000
```

## Running as a Service

### Windows Service

Use [NSSM (Non-Sucking Service Manager)](https://nssm.cc/) to run GHOSTS LITE as a Windows service:

```bash
nssm install GhostsLite "C:\Program Files\dotnet\dotnet.exe" "C:\exercise\ghosts-lite\Ghosts.Client.Lite.dll"
nssm start GhostsLite
```

### Linux Service

Create a systemd service file at `/etc/systemd/system/ghosts-lite.service`:

```ini
[Unit]
Description=GHOSTS LITE Client Service
After=network.target

[Service]
ExecStart=/usr/bin/dotnet /path/to/ghosts-lite/Ghosts.Client.Lite.dll
WorkingDirectory=/path/to/ghosts-lite
Restart=always
User=ghosts_user
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1

[Install]
WantedBy=multi-user.target
```

Enable and start the service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable ghosts-lite
sudo systemctl start ghosts-lite
```

## Monitoring and Management

GHOSTS LITE integrates with the GHOSTS API and UI, allowing you to:

- View machine status and activity
- Deploy new timelines remotely
- Monitor network traffic patterns
- Collect activity logs centrally

Access the GHOSTS UI at `http://YOUR-API-SERVER:8080` to manage your GHOSTS LITE clients.

## Troubleshooting

### GHOSTS LITE Won't Start

**Check .NET Version:**
```bash
dotnet --version
```
Ensure version 8.0 or later is installed.

**Check Logs:**
```bash
cat logs/app.log
```
Look for error messages indicating what failed.

### Cannot Connect to API

**Verify Network Connectivity:**
```bash
curl http://YOUR-API-SERVER:5000/api
```

**Check Configuration:**
Ensure `config/application.yaml` has the correct API URL with `/api` suffix:
```yaml
ApiRootUrl: http://YOUR-API-SERVER:5000/api
```

### Timeline Not Executing

**Verify Timeline File:**
Check that `config/timeline.yaml` exists and has valid YAML syntax.

**Check Handler Support:**
GHOSTS LITE only supports specific handlers. Handlers like `BrowserFirefox`, `Word`, or `Excel` from full GHOSTS will not work. Use the LITE-specific handlers instead.

## Migration from Full GHOSTS

To migrate from full GHOSTS to GHOSTS LITE:

1. **Stop the full GHOSTS client**
2. **Install GHOSTS LITE** in a separate directory
3. **Copy configuration files** (with modifications):

   - Copy `config/application.yaml`
   - Adapt `config/timeline.yaml` to use LITE-compatible handlers
4. **Test the timeline** before deploying broadly
5. **Update the API** if needed to reflect the client type change

## Performance Tuning

### Maximize Client Density

To run the maximum number of GHOSTS LITE clients on a single host:

- Reduce timeline complexity (fewer concurrent handlers)
- Increase delays between activities
- Disable verbose logging in `nlog.config`
- Monitor system resources and adjust accordingly

### Typical Capacity

On a modern server (very rough estimates):

- **Full GHOSTS**: 10-50 clients per server
- **GHOSTS LITE**: 100-500+ clients per server

Actual capacity depends on timeline complexity, network conditions, and hardware specifications.

## Getting Help

If you encounter issues:

- Check the [GHOSTS documentation](https://cmu-sei.github.io/GHOSTS/)
- Search or ask in [GitHub Discussions](https://github.com/cmu-sei/GHOSTS/discussions)
- Report bugs via [GitHub Issues](https://github.com/cmu-sei/GHOSTS/issues)
