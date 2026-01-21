# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

GHOSTS is an NPC (Non-Player Character) orchestration framework for modeling and simulating realistic user behavior on and off a computer. It is a monorepo, and consists of:

- A .NET 10 API server (command & control)
- A modern Angular 20 web interface (ghosts.ng)
- Cross-platform clients (Windows, Linux, and Universal) that execute "timelines" (activity definitions)
- Supporting services for NPC persona generation (Animator)
- Content generation (Pandora) for websites and social media platforms

**Key Technologies:**
- .NET 10 (API/Server), .NET 4.6.2 (Windows Client), .NET 8 (Lite/Universal Clients)
- PostgreSQL database
- SignalR (WebSocket) for real-time client-server communication
- Angular 20 with Material Design (ghosts.ng UI)
- Entity Framework Core + Npgsql

## Project Structure

The main components live in `src/`:

- **Ghosts.Api/** - Central .NET 10 API server (command & control)
- **Ghosts.Domain/** - Shared .NET Standard 2.0 library (timeline models, handlers, config)
- **Ghosts.Client.Windows/** - Full-featured .NET 4.6.2 Windows client
- **Ghosts.Client.Universal/** - Cross-platform client (38+ handlers)
- **Ghosts.Client.Lite/** - Lightweight .NET 8 client for resource-constrained environments
- **Ghosts.Animator/** - NPC persona generation engine
- **Ghosts.Pandora/** - Content generation server (fake web content, blog posts)
- **ghosts.ng/** - Angular 20 web UI for managing machines, timelines, NPCs, scenarios
- **apphost/** - Aspire application host infrastructure
- **tools/** - Utility tools (email generator, load tester, machine adder)

## Common Development Commands

### .NET API (Ghosts.Api)

```bash
# Build the API solution (from src/ directory)
dotnet build Ghosts.Api.sln

# Run the API server (from src/Ghosts.Api/)
dotnet run --project Ghosts.Api.csproj

# Run with specific configuration
dotnet run --configuration Release

# Create a new Entity Framework migration (from src/Ghosts.Api/)
dotnet ef migrations add <MigrationName>

# Apply database migrations
dotnet ef database update

# Run with Aspire (from src/apphost/)
dotnet run
```

### Angular UI (ghosts.ng)

```bash
# Install dependencies (from src/ghosts.ng/)
npm install

# Start development server
npm start
# or
ng serve

# Build for production
npm run build
# or
ng build

# Run tests
npm test
# or
ng test

# Watch mode (auto-rebuild on changes)
npm run watch
```

### Windows Client

```bash
# Build Windows client (from src/ directory)
dotnet build ghosts.client.windows.sln

# Build from PowerShell script (from scripts/)
.\build_windows.ps1
```

### Universal Client

```bash
# Build Universal client (from src/ directory)
dotnet build ghosts.client.universal.sln

# Build using Python script (from scripts/)
python build_universal.py
```

### Lite Client

```bash
# Build Lite client (from src/Ghosts.Client.Lite/)
dotnet build Ghosts.Client.Lite.sln

# Run build script (from src/Ghosts.Client.Lite/scripts/)
./build.sh
```

### Pandora

```bash
# Build Pandora (from src/Ghosts.Pandora/)
dotnet build Ghosts.Pandora.sln

# Run Pandora server (from src/Ghosts.Pandora/src/)
dotnet run
```

### Docker

```bash
# Build and run full stack (from src/Ghosts.Api/)
docker-compose up

# Build API Docker image (from src/)
docker build -f Dockerfile-api -t ghosts-api .
```

### Documentation

```bash
# Serve documentation locally (from repository root)
mkdocs serve

# Build documentation
mkdocs build
```

## Architecture Overview

### Timeline System

**Timelines** are the core behavior definitions for GHOSTS clients. They specify what activities NPCs should perform and when.

**Structure:** `Timeline` → `TimeLineHandlers[]` → `TimeLineEvents[]`

- **Timeline**: Container with ID, status (Run/Stop)
- **TimeLineHandler**: Defines an activity type (browser, email, command, etc.) with:
  - HandlerType (enum: BrowserChrome, Outlook, Command, Ssh, Excel, etc.)
  - Schedule/cron expressions for timing
  - UtcTimeOn/UtcTimeOff for working hours
  - Loop settings for repetition
  - HandlerArgs for custom parameters
- **TimeLineEvent**: Specific action with Command, CommandArgs, DelayBefore/DelayAfter

**Handler Types (27+):**
- **Browsers**: BrowserChrome, BrowserFirefox
- **Office Apps**: Word, Excel, PowerPoint, Outlook, Outlookv2
- **Commands**: Command, PowerShell, Bash, Curl
- **Networking**: Ssh, Sftp, Ftp, Rdp, Azure, Aws
- **System**: Reboot, Wmi, Watcher, Print
- **UI**: Clicks, Notepad, Pidgin
- **Special**: NpcSystem, ExecuteFile, LightWord/Excel/PowerPoint

**Timeline Configuration Flow:**
1. Client loads local `config/timeline.json` via `TimelineBuilder.GetTimeline()`
2. API can push timeline updates via `MachineUpdates` table and SignalR
3. Orchestrator watches for file changes (FileSystemWatcher on timeline.json and stop.txt)
4. Listener watches `instance/timeline/in/` for dropped timeline files
5. Handlers execute based on schedule/cron definitions

### Client Architecture

**Windows Client Flow (Ghosts.Client.Windows/Program.cs):**

Entry point starts multiple concurrent systems:
1. **Orchestrator** - Loads timeline, dispatches handlers, manages execution threads
2. **Listener** - Watches `instance/timeline/in/` directory for file drops
3. **Socket Connection** - SignalR WebSocket to `/clientHub`
4. **Updates Polling** - HTTP polling fallback if WebSocket fails
5. **Health Checks** - System metrics (CPU, memory, network) sent periodically
6. **Survey Collection** - Gathers local system info (processes, users, ports, etc.)

**Handler Execution:**
- All handlers extend `BaseHandler` in Ghosts.Domain
- Handlers have standardized `Execute(TimelineEvent)` method
- Results reported via `BaseHandler.Report(ReportItem)` → NLog → API
- Background queue for async result submission

**Configuration:**
- Primary: `config/application.json` or `config/application.yaml`
- Timeline: `config/timeline.json`
- Health: `config/health.json`
- Content files: Various CSV/JSON for email/blog content generation

### WebSocket/SignalR Connectivity

**Client Hub (Ghosts.Api/Hubs/ClientHub.cs):**

Hub methods clients can call:
- `SendId()` - Client self-identification
- `SendResults(TransferLogDump)` - Timeline execution results
- `SendSurvey(Survey)` - System survey data
- `SendUpdate(MachineUpdate)` - Timeline updates
- `SendHeartbeat()` - Keepalive
- `SendMessage()` / `SendSpecificMessage()` - Messaging

**Handshake:**
1. Client connects to `/clientHub`
2. Server calls `OnConnectedAsync()` → `SendId()`
3. Server maps `MachineId` to SignalR `ConnectionId` in static dictionary
4. Server returns machine GUID via `ReceiveId` callback
5. Client stores ID locally

**Connection Mapping:**
- `ConcurrentDictionary<Guid, string>` maps MachineId → ConnectionId
- Enables targeted messaging to specific clients

### Database Architecture (PostgreSQL)

**Key Entity Groups:**

**Machine Management:**
- `Machines` - Registered clients/endpoints
- `Groups` / `GroupMachines` - Machine grouping
- `MachineTimelines` - Timeline-to-machine assignments
- `MachineUpdates` - Timeline push updates

**Activity Recording:**
- `HistoryTimeline` - Individual executed activities (browser visits, commands, etc.)
- `HistoryHealth` - System health snapshots
- `HistoryTrackable` - Tagged observable actions for detection exercises

**Survey Data** (local system information):
- `Survey` - Root record per machine
- `Survey.DriveInfo`, `Survey.Interface`, `Survey.LocalProcess`, `Survey.LocalUser`, `Survey.Port`, `Survey.EventLog`

**NPC System:**
- `NpcRecord` - Generated persona profiles (full NpcProfile as JSONB)
- `NpcSocialConnection` - Relationships between NPCs
- `NpcLearning` - Topics NPCs have learned
- `NpcBelief` - NPC beliefs (step-tracked for evolution)
- `NpcPreference` / `NpcInteraction` / `NpcActivity`

**Scenarios:**
- `Scenario` - Training/exercise scenarios
- `ScenarioParameters` / `ScenarioTimeline` / `ScenarioTimelineEvent`
- `Execution` / `ExecutionEvent` / `ExecutionMetricSnapshot` - Scenario run tracking
- `Nation` / `ThreatActor` / `Inject` - Scenario context

**Administrative:**
- `Trackable` - Observable action definitions
- `Webhook` - External integrations

**Indexes:** Heavy indexing on `CreatedUtc`, `Status`, `MachineId`, social graph lookup fields

### API Layer

**Core Controllers (in Controllers/Api/):**

**Client endpoints** (used by deployed agents):
- `ClientIdController` - Machine registration
- `ClientTimelineController` - Timeline submission
- `ClientResultsController` - Activity results
- `ClientSurveyController` - Survey data
- `ClientUpdateController` - Fetch updates

**Management endpoints** (used by UI/operators):
- `MachinesController` - Machine CRUD and search
- `MachineGroupsController` - Group management
- `MachineUpdatesController` - Push timeline updates
- `TimelinesController` - Timeline template management
- `MachineTimelinesController` - Timeline assignments
- `TrackablesController` - Observable action management
- `NpcsController` / `NpcsGenerateController` - NPC management/generation
- `ScenariosController` / `ExecutionsController` - Scenario management
- `AnimationsController` / `AnimationJobsController` - Animator job control
- `WebhooksController` - Webhook configuration

**Documentation:** Swagger/OpenAPI at `/swagger/v9/swagger.json`

### Animator - NPC Persona Generation

**Npc.cs generation engine** creates complete profiles:
- **Identity**: Name, email, phone, address, username, password
- **Military**: Rank, unit, branch, MOS, billet (US military context)
- **Career**: Employment history, job titles, strengths/weaknesses
- **Physical**: Height, weight, biological sex, birthdate, photo
- **Social**: Family relationships, social accounts
- **Financial**: Credit cards, net worth, debt
- **Health**: Medical conditions, mental health
- **Education**: Military education, degrees
- **Preferences**: Food, travel, music, etc.
- **Insider Threat**: Potential vulnerability indicators

**Configuration:**
- `NpcGenerationConfiguration` for customizing branch, username patterns
- Uses data files from `config/` (ranks, bases, units, medical conditions, etc.)
- Profiles stored as JSONB in PostgreSQL `npcs.npc_profile`

### Pandora - Content Generation

Provides dynamic web content for realistic browser simulation:
- Multiple database backends (PostgreSQL, SQLite)
- Environment variable configuration (`MODE_TYPE`, `SITE_TYPE`, `ARTICLE_COUNT`)
- Generates fake blog posts, news articles, social content
- Separate web service (not embedded in API)

## Key Design Patterns

1. **Service Layer**: `IMachineService`, `ITimelineService`, dependency injection
2. **Hub-based Real-time**: SignalR for two-way communication with automatic reconnection
3. **Queue-based Async**: `BackgroundQueue` prevents blocking handler threads
4. **File Watcher**: `FileSystemWatcher` for config/timeline hot-reloading
5. **Handler Plugin**: All handlers extend `BaseHandler` with standardized `Execute()` method
6. **Configuration Management**: Singleton pattern, JSON/YAML support, environment variable overrides
7. **Domain Model Separation**: `Ghosts.Domain` netstandard2.0 library shared by all components

## Data Flow - Timeline Execution

```
API Server (PostgreSQL)
    ↓ MachineUpdate created
    ↓ SignalR ClientHub sends to specific client
    ↓
Client receives via HubConnection
    ↓ Orchestrator processes TimelineHandler
    ↓ Selects Handler class (BrowserChrome, Outlook, etc.)
    ↓ Handler.Execute(TimelineEvent)
    ↓ Handler.Report(ReportItem) → NLog
    ↓ Result queued to BackgroundTaskQueue
    ↓ Queue worker sends via ClientHub.SendResults()
    ↓
API Server
    ↓ ClientResultsService.ProcessResultAsync()
    ↓ Stores in HistoryTimeline table
    ↓ Updates Machine.LastReportedUtc
    ↓ Webhooks triggered (if configured)
```

## Code Organization Notes

### Domain Models (Ghosts.Domain)

**Key namespaces:**
- `Ghosts.Domain.Messages` - Timeline, Handler, Result models
- `Ghosts.Domain.Code` - Handler implementations (BaseHandler, BrowserChrome, etc.)
- `Ghosts.Domain.Code.Helpers` - Utilities (TimelineBuilder, Jitter, etc.)

**Handler Hierarchy:**
- `BaseHandler` - Abstract base with Execute() and Report()
- Specific handlers in `Ghosts.Domain/Code/Handlers/`
- Handler results flow through `ResultsQueueService`

### API Services (Ghosts.Api/Infrastructure)

**Service implementations:**
- `MachineService` - Machine CRUD operations
- `TimelineService` - Timeline management
- `NpcService` - NPC generation/management
- `BackgroundQueue` - Async task queue
- Dependency injection in `Program.cs`

### Angular UI (ghosts.ng)

**Structure:**
- `src/app/` - Main Angular application
- `src/app/components/` - Reusable UI components
- `src/app/services/` - API communication services
- Material Design components throughout
- SignalR HubConnection for real-time updates

## Configuration Files

### API (Ghosts.Api/appsettings.json)
- ConnectionStrings (PostgreSQL)
- CORS policy
- Logging configuration
- OpenAI/LLM settings (if using AI features)

### Client (config/)
- `application.json` / `application.yaml` - Main client config
  - ApiRootUrl
  - Sockets (WebSocket settings)
  - ClientResults (reporting settings)
  - ClientUpdates (polling settings)
  - Survey (system info collection)
- `timeline.json` - Default timeline
- `health.json` - Health check configuration

### Animator (Ghosts.Api/config/)
- 27+ JSON/TXT files for NPC generation
- Military data: ranks, units, bases, MOS, education
- Population data: addresses, cities, demographics
- Preferences: meal preferences, universities, etc.

## Testing and Development

### Running Tests

```bash
# Run .NET tests for a specific project
dotnet test src/Ghosts.Api.sln

# Run Angular tests
cd src/ghosts.ng
npm test
```

### Development Workflow

1. **API Development:**
   - Modify code in `src/Ghosts.Api/`
   - Run migrations if database changes: `dotnet ef migrations add <Name>`
   - Test with Swagger UI at `http://localhost:5000/swagger`

2. **Client Development:**
   - Modify handlers in `src/Ghosts.Domain/Code/Handlers/`
   - Test timeline execution with local client instance
   - Check logs in `logs/` directory

3. **UI Development:**
   - `cd src/ghosts.ng`
   - `npm start` for development server
   - Hot reload enabled for rapid iteration

### Debugging

- **API**: Use VS Code with C# Dev Kit or Visual Studio
- **Clients**: Attach debugger or use `--debug` flag for verbose logging
- **Angular**: Browser DevTools, Angular DevTools extension
- **Logs**: Check NLog outputs in `logs/` directory
- **Database**: Query PostgreSQL directly for debugging state

## Important Notes for Development

1. **Timeline Changes:** Clients detect timeline file changes via FileSystemWatcher - no restart needed
2. **Handler Registration:** New handlers must be added to HandlerType enum in Ghosts.Domain
3. **Database Migrations:** Always create migrations for schema changes, never modify database directly
4. **SignalR Connections:** Mapped by MachineId in static dictionary - careful with connection lifecycle
5. **Security:** No built-in authentication - expected to be fronted by auth proxy
6. **CORS:** Configure in appsettings.json for cross-origin API access
7. **Client ID:** Machine identification via headers or local storage, registered in Machines table
8. **Async Handlers:** Use BackgroundQueue for long-running operations to avoid blocking

## Scripts and Automation

- `scripts/build_windows.ps1` - Build Windows client
- `scripts/build_universal.py` - Build Universal client
- `scripts/horde/horde.py` - Bulk operation scripts (Horde automation)
- `.devcontainer/postcreate.sh` - Devcontainer initialization

## Extension Points

1. **New Handler Types:** Extend `BaseHandler` in Ghosts.Domain, add to HandlerType enum
2. **Custom NPC Attributes:** Modify `Npc.cs` in Ghosts.Animator
3. **Custom Webhooks:** Implement `IWebhookService` for external integrations
4. **Custom Timelines:** Create JSON timeline files with handler sequences
5. **Custom Content:** Extend Pandora for new content generation patterns

## Related Documentation

- Full docs: https://cmu-sei.github.io/GHOSTS/
- API docs: `/swagger` when API is running
- MkDocs source: `docs/` directory
