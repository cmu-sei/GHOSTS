# GHOSTS MCP Server

This is a small MCP server for using GHOSTS through an MCP client. It exposes read-only tools for common API lists, one action that sends a browser timeline to a machine, and four tools for authoring a scenario as a document.

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

### Scenario documents

A scenario document is one exercise as a single versioned, diffable JSON file — the schema and its
tooling are in [`schemas/scenario-document/`](../../../schemas/scenario-document/). These four are the
only tools an authoring agent needs, and they are deliberately not a CRUD surface over the scenario
tables: an agent edits the document and imports it, so what a reviewer signs off is what gets loaded.

| Tool | Reads or writes | What it does |
|---|---|---|
| `scenario_document_validate` | **writes nothing, ever** | Returns the validator's findings: tier 1 schema, tier 2 referential, and with `dryRun: true` tier 4, which loads and compiles the document inside a transaction that is always rolled back. Safe on a half-finished draft, which is what makes it the tool to call after every edit. |
| `scenario_document_import` | writes, or refuses | Runs the same validator and **refuses on any finding of severity `error`**, returning the findings and no id. On success, the new scenario id. |
| `scenario_document_export` | read-only | One scenario as a canonical document: no database ids, no timestamps, no run state. Two exports of an unchanged scenario are byte-identical, and the output is valid input to import. |
| `attack_technique_lookup` | read-only | Resolves ATT&CK techniques by id or by a fragment of a name, with MITRE's revoked and deprecated flags. |

A finding carries `tier` (1–4), `severity` (`error` blocks import; `warning` and `info` do not), a
stable `code`, a JSON `path` pointer into the document, a `message`, and sometimes a `hint`.

`attack_technique_lookup` reads `GET /api/attack/techniques` rather than carrying its own copy of the
index. That is the point: the API serves the same embedded `corpus/attack-index.json` that the
validator's tier 2 checks against, so the tool cannot tell an author a technique is fine and then have
the import reject it. It is marked `OpenWorld = false` for the same reason — the set of answers is a
committed, versioned corpus, not the open internet — and every response names the MITRE bundle commit
it was built from.
