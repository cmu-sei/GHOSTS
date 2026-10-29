// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.Terrain;

// ─────────────────────────────────────────────────────────────────────────────
// What GHOSTS needs from a terrain provider (GraySpace), version 1.
// docs/core/api/terrain.md describes each call; GraySpaceStub implements them in memory.
// JSON is camelCase. Every call carries the X-Api-Key header.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>A base network the provider can build enclaves on. GET /api/v1/bases</summary>
public record TerrainBase(string Id, string Name, string Version, string ProxmoxNode, string Status);

/// <summary>The base's own hosts and segments, so authoring can name them. GET /api/v1/bases/{baseId}/snapshot</summary>
public record TerrainBaseSnapshot(string BaseId, string Version, List<TerrainSegment> Segments, List<TerrainHost> Hosts);

public record TerrainSegment(string Name, string Cidr = null, string Description = null);

public record TerrainHost(string Name, string Segment = null, string Os = null, string Role = null, List<string> Services = null);

/// <summary>An enclave the provider knows how to build, and the base it needs. GET /api/v1/enclaves</summary>
public record TerrainEnclave(string Name, string Version, string BaseId, int HostCount);

/// <summary>
/// Build an enclave on a base. POST /api/v1/instances. Expected is what the scenario document says the enclave
/// holds, so the provider can check it; IdempotencyKey makes a retried request return the same instance.
/// </summary>
public record CreateTerrainInstance(string BaseId, string Enclave, string IdempotencyKey, string Label = null, TerrainExpectation Expected = null);

public record TerrainExpectation(List<TerrainSegment> Segments = null, List<TerrainHost> Hosts = null);

/// <summary>A build in progress, or done. Returned by create, GET /api/v1/instances/{id} and DELETE /api/v1/instances/{id}.</summary>
public record TerrainInstance(
    string Id,
    string BaseId,
    string Enclave,
    string Status,
    int Progress,
    string JobId,
    List<string> Log,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string Error = null);

public static class TerrainStatus
{
    public const string Queued = "queued";
    public const string Building = "building";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string TearingDown = "tearing-down";
    public const string Deleted = "deleted";

    public static bool InProgress(string status) => status is Queued or Building or TearingDown;
}

/// <summary>Every host in a built instance, and the shared services on it. GET /api/v1/instances/{id}/manifest</summary>
public record TerrainManifest(
    string InstanceId,
    string BaseId,
    string Enclave,
    string DirectoryDomain,
    List<TerrainManifestHost> Hosts,
    List<TerrainSite> Sites);

/// <summary>
/// One host. Name equals the hostname the host reports, so a GHOSTS client on it matches by name.
/// GhostsClient says the host runs a GHOSTS client; ParticipantWorkstation says it is a console for a person.
/// </summary>
public record TerrainManifestHost(
    string Name,
    string Segment,
    string RangeIp,
    string ManagementIp,
    string OsFamily,
    string OsVersion,
    string Role,
    string ProxmoxNode,
    int Vmid,
    bool GhostsClient,
    bool ParticipantWorkstation);

/// <summary>A service site on the instance, such as a Pandora site, the mail server or the chat server.</summary>
public record TerrainSite(string Name, string Kind, string Url);

/// <summary>The error body for any non-2xx response.</summary>
public record TerrainError(string Code, string Message);

/// <summary>A terrain provider, as GHOSTS uses one. GraySpaceTerrainProvider talks to the HTTP API above.</summary>
public interface ITerrainProvider
{
    Task<List<TerrainBase>> ListBasesAsync(CancellationToken ct);
    Task<TerrainBaseSnapshot> GetBaseSnapshotAsync(string baseId, CancellationToken ct);
    Task<List<TerrainEnclave>> ListEnclavesAsync(CancellationToken ct);
    Task<TerrainInstance> CreateInstanceAsync(CreateTerrainInstance request, CancellationToken ct);
    Task<TerrainInstance> GetInstanceAsync(string instanceId, CancellationToken ct);
    Task<TerrainManifest> GetManifestAsync(string instanceId, CancellationToken ct);
    Task<TerrainInstance> DeleteInstanceAsync(string instanceId, CancellationToken ct);
}

/// <summary>Raised when the provider answers with an error, carrying its HTTP status and error body.</summary>
public class TerrainProviderException(int status, string code, string message) : Exception($"Terrain provider error {status} {code}: {message}")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

/// <summary>Where the provider is. Empty BaseUrl means none is configured. In development (the Aspire AppHost) BaseUrl points at GHOSTS's own stub: http://localhost:5000/stubs/grayspace</summary>
public class TerrainProviderOptions
{
    public const string Section = "TerrainProvider";
    public string Name { get; set; } = "grayspace";
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Serve the in-memory GraySpace stub at /stubs/grayspace/api/v1.</summary>
    public bool EnableStub { get; set; }

    /// <summary>Seconds a stub build takes, so polling sees progress.</summary>
    public int StubBuildSeconds { get; set; } = 30;
}
