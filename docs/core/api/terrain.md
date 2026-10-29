# Terrain (GraySpace)

GraySpace builds the network an execution runs on. GHOSTS is a client of its **Terrain API 1.3.0**. The contract is
GraySpace's own `openapi/grayspace-v1.json`, with the enclave spec in `grayspace-slice-v1.json` and the events in
`grayspace-events-v1.json`. GHOSTS keeps a copy of all three in `src/Ghosts.Api.Tests/GraySpace`, and its tests
check what GHOSTS sends, and what its simulator answers, against them.

| Term | Meaning |
|---|---|
| Base | GraySpace's simulated internet: ISPs, routers, links, core DNS and CAs. One is deployed per GraySpace, for example `grayspace/2026.10` |
| Enclave | What a scenario adds at the edge of the base: sites, segments and hosts. GraySpace's API calls it a *slice* |
| Instance | An enclave built for one execution. GHOSTS names it `g{executionId}`, which is also the prefix of every hostname in it |

## The calls

Every call sends `Authorization: Bearer <token>`, GraySpace's `GRAYSPACE_API_TOKEN`. JSON is snake_case. An error is
FastAPI's `{"detail": "..."}`, or a list of `{loc, msg}` for a 422. GHOSTS reports it as a provider error
(`unauthorized`, `not_found`, `conflict`, `invalid_request`, `unavailable`), or as `unreachable` when GraySpace
doesn't answer at all.

| Call | GHOSTS uses it to |
|---|---|
| `GET /api/v1/base` | Check a base is deployed, and that it is the one the scenario names |
| `GET /api/v1/capabilities` | Pick images and roles for hosts, and check GraySpace can run jobs |
| `GET /api/v1/slices` | List stored enclaves for the authoring agent |
| `PUT /api/v1/slices/{slug}` | Store the scenario's enclave. It returns a version and findings |
| `POST /api/v1/slices/{slug}/validate` | Check a draft's terrain while authoring. Nothing is stored |
| `POST /api/v1/instances` | Build the enclave for an execution: `{slice, version, prefix: "g{id}"}` returns a job |
| `GET /api/v1/jobs/{id}`, `/jobs/{id}/log` | Follow the build's stages and log |
| `POST /api/v1/jobs/{id}/retry` | Resume a failed build from the stage that failed |
| `GET /api/v1/instances/{id}` | Read the instance's state and, once ready, its manifest |
| `DELETE /api/v1/instances/{id}` | Tear the enclave down. The base stays |
| `GET /api/v1/map?instance=g{id}` | The map layer: routers, links, and this enclave's sites |
| `GET /api/v1/events` (websocket) | Live job, instance, feature and guest updates |

## From a scenario to an enclave

GHOSTS turns the scenario into GraySpace's enclave spec itself. GraySpace never reads GHOSTS documents.

| Scenario document | Enclave spec |
|---|---|
| `terrain.reference.enclave` (or the scenario's slug) | `slug`. `owner` is `ghosts` |
| `terrain.reference.base` | Checked against `GET /base`; a different deployed base refuses the build |
| `terrain.segments[]` | One NAT site, `main`, attached to the first ISP `/capabilities` lists, where GraySpace picks the router with the most free addresses. GraySpace refuses an attach that names nothing. Private CIDRs are kept; any other is dropped and GraySpace allocates one |
| `terrain.hosts[]` | A host each. `os` picks an image from `/capabilities`, Ubuntu when GraySpace has none for that OS. `role`, `description` and `services` pick the roles `web`, `mail`, `dns` and `file`. A domain controller gets no `ad-dc` role, with a note: the role needs a realm, a domain and an admin password the document doesn't hold |
| A host whose role is a participant console | Labelled `ghosts.console: "true"`, with no GHOSTS client |
| `population.pools[]` placed on hosts | Those hosts get the `ghosts-client` role and the label `ghosts.pool: <role>` |
| `population.pools[]` otherwise | Counted `ghosts-client` hosts on the pool's segments, labelled with the pool |
| — | `role_vars.ghosts-client`: `client_url`, `api_url`, `api_host`, from settings |

`terrain_validate` and a refused build show GraySpace's findings, with their codes and the paths in the spec. They
also show notes on anything GHOSTS couldn't carry over as written.

## A build

1. **Build terrain** (`POST /api/executions/{id}/terrain`) checks the base and capabilities, stores the enclave, and
   creates instance `g{id}`. Error findings refuse the build and stay on the terrain, so the Terrain tab shows them.
   This applies to findings from the store, and to those GraySpace returns at create (422), when it checks the
   prefix and the node's capacity.
2. The terrain follows the job GHOSTS asked for, then the instance. GraySpace runs one job at a time and changes an
   instance's state only when a job starts, so a teardown or retry waiting its turn would otherwise look like nothing
   happened.
   - A create job that is queued is **queued**. One that is running is **building**, until the instance is ready.
   - A failed or cancelled create is **failed**.
   - A destroy job that is queued or running is **tearing-down**. One that succeeded is **deleted**, and one that
     failed is **failed**.

   Progress is the job's stage, and the log is read with `follow=false`. `TerrainStatusPoller` refreshes every five
   seconds, and every job or instance event refreshes at once.
3. Once ready, the manifest is kept on the execution: sites, hosts, DNS records and certificates. Each host has the
   hostname it reports (`g{id}-<name>`), its addresses, roles and labels.
4. **Build terrain** after a failed build retries the failed job, if the enclave hasn't changed. After a failed
   teardown, it refuses: tear down again. After a teardown, it creates a new instance.
5. **Tear down** (`DELETE /api/executions/{id}/terrain`) runs GraySpace's destroy job, and cancels a build still
   running. An instance GraySpace has already destroyed, or no longer knows, is marked **deleted**.

NPCs bind to the manifest's `ghosts-client` hosts by the hostname they report: the `ghosts-client` role writes
`Id.Format: hostname`. A pool takes the hosts labelled with it first, and consoles get no NPC. A scenario event's
`targets` use the document's own host and segment names, which GHOSTS maps to what GraySpace built.

## In GHOSTS

| Endpoint | |
|---|---|
| `POST`, `GET`, `DELETE /api/executions/{id}/terrain` | Build, read and tear down the execution's enclave |
| `GET /api/executions/{id}/terrain/map` | GraySpace's map layer for the execution map, relayed as GeoJSON |
| `GET /api/executions/{id}/terrain/player-view` | For Crucible Player, until its API is settled: the participant consoles, with hostname, segment, IP and management IP. 409 until the terrain is ready |

The execution map draws the layer and applies GraySpace's `feature.update` and `guest.update` events live. GHOSTS
relays every event from `/api/v1/events` to the frontend as `TerrainEvent` on the execution hub.

The authoring agent has two read-only tools:
- `terrain_capabilities` returns the base, the images, roles and ISPs, and the stored enclaves.
- `terrain_validate` checks a validated draft's terrain as GraySpace would build it.

## Settings

In `appsettings.json`, or as environment variables:

| Setting | Default | |
|---|---|---|
| `TerrainProvider__BaseUrl` | empty | GraySpace's address, without `/api/v1`. Empty: terrain calls fail with `not_configured` |
| `TerrainProvider__Token` | empty | GraySpace's `GRAYSPACE_API_TOKEN` |
| `TerrainProvider__ClientUrl` | empty | The GHOSTS client archive enclave hosts download (the `ghosts-client` role's `client_url`) |
| `TerrainProvider__GhostsApiUrl` | empty | The GHOSTS API as enclave hosts reach it over management, such as `http://ghosts:5000/api` |
| `TerrainProvider__EnableStub` | `false` | `true` serves the simulator at `/stubs/grayspace` |
| `TerrainProvider__StubBuildSeconds` | `30` | How long a simulated build takes |

## Development: the simulator

The Aspire AppHost points GHOSTS at its own GraySpace simulator, with the token `dev-token`. It answers the calls
above in GraySpace's shapes, and its tests check that against the contract snapshot. It builds nothing:
- It refuses an attach that names nothing, as GraySpace does.
- It checks the findings GHOSTS is most likely to meet: unknown image or role, a role the image can't run, a missing
  role variable, an unknown or duplicate name, a bad CIDR. It refuses a create with errors (422, with the findings).
- It runs jobs one at a time, each through GraySpace's stages in `StubBuildSeconds`. An instance's state changes when
  its job starts, and a destroy cancels a running create, as in GraySpace.
- It allocates a manifest as GraySpace names one.
- It streams a job's log until the job ends unless `follow=false`.
- It serves the map layer and the events socket.

GraySpace's own simulated mode stores and validates enclaves, but can't run their jobs.

Under Docker Compose, set `TERRAIN_STUB=true`, `TERRAIN_BASE_URL=http://localhost:5000/stubs/grayspace` and
`TERRAIN_TOKEN=dev-token`. Against a real GraySpace, set `TERRAIN_BASE_URL`, `TERRAIN_TOKEN`, `TERRAIN_CLIENT_URL`
and `TERRAIN_GHOSTS_API_URL`.

```bash
curl -s -H 'Authorization: Bearer dev-token' http://localhost:5000/stubs/grayspace/api/v1/base
curl -s -H 'Authorization: Bearer dev-token' http://localhost:5000/stubs/grayspace/api/v1/capabilities
```
