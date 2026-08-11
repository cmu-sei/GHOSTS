# GHOSTS MCP Server

This is a small MCP server for using GHOSTS through an MCP client. It exposes read-only tools for common API lists and one action that sends a browser timeline to a machine.

## Run

```bash
dotnet run --project src/tools/ghosts.tools.mcp/ghosts.tools.mcp.csproj
```

For HTTP transport, used by Aspire:

```bash
ASPNETCORE_URLS=http://localhost:5055 dotnet run --project src/tools/ghosts.tools.mcp/ghosts.tools.mcp.csproj -- --http
```

From n8n running in Aspire, use:

```text
http://mcp:5055/mcp
```

By default, the server talks to `http://localhost:5000`. Override that with:

```bash
GHOSTS_API_BASE_URL=http://localhost:5000 dotnet run --project src/tools/ghosts.tools.mcp/ghosts.tools.mcp.csproj
```

If your deployment uses an auth proxy, set `GHOSTS_API_TOKEN` to send a bearer token.

## Tools

- `api_get_base_url`
- `api_check_connection`
- `machine_list`
- `machine_get_by_id`
- `npc_list`
- `npc_get_by_id`
- `scenario_list`
- `browser_timeline_build`
- `browser_timeline_send`
