# Terrain providers (GraySpace)

GHOSTS asks a terrain provider to build the network an execution runs on. GraySpace is the provider: it builds an
enclave on a base network on a Proxmox node, and tells GHOSTS what it built. This page is the contract, version 1:
the calls GHOSTS makes and what it expects back.

GHOSTS ships an in-memory stand-in at `/stubs/grayspace/api/v1`, so the whole flow runs without Proxmox. Its calls
appear in the GHOSTS Swagger page under **GraySpaceStub**, where you can try each one.

## The calls

| # | Call | GHOSTS uses it to |
|---|---|---|
| 1 | `GET /api/v1/bases` | List the bases a scenario can name |
| 2 | `GET /api/v1/bases/{baseId}/snapshot` | Read a base's segments and hosts while authoring |
| 3 | `GET /api/v1/enclaves` | List the enclaves it can build, and find the base an enclave needs |
| 4 | `POST /api/v1/instances` | Build an enclave on a base for one execution |
| 5 | `GET /api/v1/instances/{id}` | Follow the build's status, progress and job log |
| 6 | `GET /api/v1/instances/{id}/manifest` | Read every host it built, to bind NPCs and set up participant access |
| 7 | `DELETE /api/v1/instances/{id}` | Tear the enclave down after the execution. The base stays |

Every call sends `X-Api-Key`. JSON is camelCase. A non-2xx response carries an error body:

```json
{ "code": "instance_not_found", "message": "No instance inst-9" }
```

## 1. List bases

`GET /api/v1/bases` → `200`

```json
[{ "id": "office-base", "name": "Office base", "version": "2", "proxmoxNode": "pve-01", "status": "running" }]
```

## 2. A base's snapshot

`GET /api/v1/bases/{baseId}/snapshot` → `200`, or `404 base_not_found`

```json
{
  "baseId": "office-base",
  "version": "2",
  "segments": [{ "name": "Base internet", "cidr": "198.51.100.0/24", "description": "ISPs, DNS and the Pandora sites" }],
  "hosts": [{ "name": "mail-01", "segment": "Base internet", "os": "linux", "role": "mail", "services": ["smtp", "imap"] }]
}
```

## 3. List enclaves

`GET /api/v1/enclaves` → `200`

```json
[{ "name": "office-enclave", "version": "1", "baseId": "office-base", "hostCount": 5 }]
```

## 4. Build an instance

`POST /api/v1/instances` → `202`

```json
{
  "baseId": "office-base",
  "enclave": "office-enclave",
  "idempotencyKey": "execution-7",
  "label": "office-r1",
  "expected": {
    "segments": [{ "name": "Office LAN", "cidr": "10.20.0.0/24" }],
    "hosts": [{ "name": "desk-01", "segment": "Office LAN", "os": "ubuntu", "role": "workstation" }]
  }
}
```

- `idempotencyKey` is `execution-{id}`, or `execution-{id}-after-{instanceId}` for a rebuild after a teardown or a
  failed build. Repeating a request with the same key must return the same instance, not start a second build.
- `expected` is what the scenario document says the enclave holds. GraySpace may use it to check the enclave, or
  ignore it. The stub builds exactly these hosts.

The response is an instance, as in call 5.

## 5. An instance

`GET /api/v1/instances/{id}` → `200`, or `404 instance_not_found`

```json
{
  "id": "inst-3f2a91c0",
  "baseId": "office-base",
  "enclave": "office-enclave",
  "status": "building",
  "progress": 40,
  "jobId": "job-3f2a91c0",
  "log": ["queued office-enclave on office-base", "building 5 hosts"],
  "createdAt": "2026-10-28T09:00:00Z",
  "updatedAt": "2026-10-28T09:00:12Z",
  "error": null
}
```

`status` is one of `queued`, `building`, `ready`, `failed`, `tearing-down`, `deleted`. GHOSTS polls every five seconds
while the status is `queued`, `building` or `tearing-down`, and reads the manifest once it is `ready`.

## 6. The manifest

`GET /api/v1/instances/{id}/manifest` → `200`, `409 not_ready` before the build is ready, or `404 instance_not_found`

```json
{
  "instanceId": "inst-3f2a91c0",
  "baseId": "office-base",
  "enclave": "office-enclave",
  "directoryDomain": "office.local",
  "hosts": [{
    "name": "desk-01",
    "segment": "Office LAN",
    "rangeIp": "10.20.0.10",
    "managementIp": "172.30.0.10",
    "osFamily": "linux",
    "osVersion": "ubuntu-24.04",
    "role": "workstation",
    "proxmoxNode": "pve-01",
    "vmid": 1001,
    "ghostsClient": true,
    "participantWorkstation": false
  }],
  "sites": [{ "name": "Chirper", "kind": "pandora", "url": "http://chirper.office.local" }]
}
```

- `name` must equal the hostname the host reports. GHOSTS matches its clients to hosts by exact name, ignoring case.
- `ghostsClient` says the host runs a GHOSTS client, so NPCs can be bound to it.
- `participantWorkstation` says the host is a console for a person, reached through Crucible Player.
- `sites` lists the instance's service sites: Pandora sites, the mail server, the chat server.

## 7. Tear down

`DELETE /api/v1/instances/{id}` → `202` with the instance, now `tearing-down`, or `404 instance_not_found`

## In GHOSTS

| GHOSTS endpoint | What it does |
|---|---|
| `POST /api/executions/{id}/terrain` | Builds the execution's enclave (call 4). Body `{ "baseId": null, "enclave": null }` takes both from the scenario's terrain reference |
| `GET /api/executions/{id}/terrain` | The execution's terrain: instance, status, progress, job log and manifest |
| `DELETE /api/executions/{id}/terrain` | Tears the enclave down (call 7) |
| `GET /api/executions/{id}/terrain/player-view` | For Crucible Player, until its API is settled: the ready manifest's participant consoles, with name, segment, range and management IPs, OS, Proxmox node and VMID. 409 until the terrain is ready |

Settings, in `appsettings.json` or as environment variables:

| Setting | Default | |
|---|---|---|
| `TerrainProvider__BaseUrl` | empty | The GraySpace address. Empty, terrain calls fail with `not_configured` |
| `TerrainProvider__ApiKey` | empty | Sent as `X-Api-Key` |
| `TerrainProvider__EnableStub` | `false` | `true` serves the stub at `/stubs/grayspace` |
| `TerrainProvider__StubBuildSeconds` | `30` | How long a stub build takes |

## Try it against the stub

The Aspire AppHost turns the stub on and points `BaseUrl` at it, with the key `dev-terrain-key`. Under Docker Compose, set `TERRAIN_STUB=true`, `TERRAIN_BASE_URL=http://localhost:5000/stubs/grayspace` and `TERRAIN_API_KEY=dev-terrain-key`.

```bash
curl -s -H 'X-Api-Key: dev-terrain-key' http://localhost:5000/stubs/grayspace/api/v1/bases
curl -s -X POST -H 'X-Api-Key: dev-terrain-key' -H 'Content-Type: application/json' \
  -d '{"baseId":"office-base","enclave":"office-enclave","idempotencyKey":"try-1"}' \
  http://localhost:5000/stubs/grayspace/api/v1/instances
```
