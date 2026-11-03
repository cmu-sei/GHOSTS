# Installing the GHOSTS client on enclave hosts

These scripts put a GHOSTS NPC client on a freshly built host, pointed at the GHOSTS API, for hosts GHOSTS doesn't
build through GraySpace or for baking into a host image. On an enclave GHOSTS builds through GraySpace (Terrain API
1.3.0), GraySpace's own `ghosts-client` role does this: it downloads the client archive from
`TerrainProvider:ClientUrl` and reports to `TerrainProvider:GhostsApiUrl` by hostname
([Terrain (GraySpace)](../../docs/core/api/terrain.md)).

## What a host needs

- **Outbound TCP 5000** to the GHOSTS API on the management network, for REST and the SignalR socket. No inbound port.
- **A hostname equal to its name in the terrain manifest.** GHOSTS matches clients to hosts by exact name, ignoring case.
- **A non-root account** for the client. On Windows, an interactive logon for that account.

## Linux

Build the package once:

```bash
dotnet publish src/Ghosts.Client.Universal -c Release -o out/client
(cd out/client && zip -qr ../ghosts-client-universal.zip .)
```

Install on each host, as root:

```bash
./linux/install.sh --api http://ghosts-api:5000/api --package ghosts-client-universal.zip --user ghosts
```

It unpacks the client to `/opt/ghosts`, writes `config/application.json` from `application.json` here, and installs
and starts `ghosts-client.service`. The host needs `unzip` and the .NET 10 runtime at `/usr/bin/dotnet`.

`sh linux/test-install.sh` checks the installer against a temporary root.

## Windows

Install on each host, as an administrator:

```powershell
.\windows\install.ps1 -ApiRootUrl http://ghosts-api:5000/api -Package .\ghosts-client-windows.zip -RunAs OFFICE\npc01
```

It unpacks the client to `C:\ghosts`, sets the API in the package's own config, and registers a scheduled task that
starts `ghosts.exe` when the account logs on. The Windows script has not been run in the devcontainer, which has no
PowerShell.

## The config it writes

`application.json` here is the Universal client's default with four changes:

| Key | Value | Why |
|---|---|---|
| `ApiRootUrl` | the `--api` value | where the client reports |
| `Id.IsEnabled` | `false` | report the hostname, not a VMware guestinfo name |
| `Sockets.IsEnabled` | `true` | receive dispatched timeline items as they happen |
| `ClientResults.CycleSleep`, `ClientUpdates.CycleSleep` | `60000` | report and poll every minute instead of every five |
