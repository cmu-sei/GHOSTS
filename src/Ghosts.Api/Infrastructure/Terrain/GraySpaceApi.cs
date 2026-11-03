// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.Terrain;

// ─────────────────────────────────────────────────────────────────────────────
// GraySpace Terrain API 1.3.0 (GraySpace openapi/grayspace-v1.json), the parts GHOSTS uses. A snapshot of the
// contract is in src/Ghosts.Api.Tests/GraySpace. Wire JSON is snake_case; every call carries
// Authorization: Bearer <token>. GraySpace calls what GHOSTS names an enclave a "slice": GHOSTS turns a scenario's
// terrain into a slice spec, stores it, and builds an instance of it. GraySpaceSimulator answers the same calls.
// Required fields that may be null are written as null (JsonIgnoreCondition.Never); other nulls are left out.
// ─────────────────────────────────────────────────────────────────────────────

public record BaseVersionRef(string Name, string CsvSha256, string Scope);

public record RouterHealth(int Expected, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Running, List<string> NotRunning = null);

public record BgpHealth(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Established);

/// <summary>The deployed base and its health. GET /api/v1/base. Status: not_deployed, healthy, degraded or unknown.</summary>
public record DeployedBase([property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] BaseVersionRef Version, string Status, RouterHealth Routers, BgpHealth Bgp);

public record GraySpaceImage(string Name, string Type, string Source, string OsFamily = null, string Description = null, List<string> Features = null);

public record GraySpaceRole(string Name, string Description, List<string> OsFamilies);

public record HostFeature(string Name, string ImageType, string Description);

public record DnsServer(string Hostname, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Ip, string Router, string Isp);

public record DnsCapabilities(List<string> Tlds, List<DnsServer> Hosting, List<DnsServer> Resolvers);

public record CertificateAuthority(string Hostname, string Name, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Ip, string Router, string Isp);

public record IspCapability(string Name, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Asn, List<string> Locations);

/// <summary>What an enclave can use: images, roles, features, whether jobs run, DNS and CAs. GET /api/v1/capabilities</summary>
public record GraySpaceCapabilities(
    BaseVersionRef Base,
    List<GraySpaceImage> Images,
    List<GraySpaceRole> Roles,
    List<HostFeature> HostFeatures,
    List<string> SliceFeatures,
    bool Jobs,
    string ManagementSubnet,
    string SlicePool,
    DnsCapabilities Dns,
    List<CertificateAuthority> Cas,
    List<IspCapability> Isps,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] JsonElement? Capacity,
    string JobsUnavailableReason = null);

public record SliceVersion(string Version, DateTime Created);

/// <summary>A stored enclave (GraySpace: slice) and its versions. GET /api/v1/slices. Kind is base or slice.</summary>
public record SliceSummary(string Slug, string Kind, string Description, string Owner = null, string Latest = null,
    List<SliceVersion> Versions = null, List<string> Instances = null);

/// <summary>Where a site or internet host joins the base: an ISP and/or location, or a router. Empty: GraySpace picks.</summary>
public record SliceAttach(string Isp = null, string Location = null, string Router = null);

public record SliceSegment(string Name, string Cidr = null);

public record SliceSite(string Name, SliceAttach Attach, List<SliceSegment> Segments, bool? Routed = null);

public record SliceSize(int Cores, int Memory, int Disk);

public record SliceHost(
    string Name,
    string Image,
    int? Count = null,
    string Segment = null,
    SliceAttach Internet = null,
    List<string> Roles = null,
    SliceSize Size = null,
    Dictionary<string, string> Labels = null,
    Dictionary<string, string> Env = null,
    bool? Publish = null);

/// <summary>An enclave as GraySpace builds it: sites and segments, hosts, and the variables its roles read (grayspace-slice-v1.json).</summary>
public record SliceSpec(
    string Slug,
    string Prefix,
    string Description,
    string Owner,
    List<SliceSite> Sites,
    List<SliceHost> Hosts,
    Dictionary<string, Dictionary<string, string>> RoleVars = null)
{
    [JsonPropertyName("apiVersion")] public string ApiVersion { get; init; } = "grayspace/v1";
    public string Kind { get; init; } = "Slice";
}

/// <summary>A problem GraySpace found in a spec. Severity error blocks an instance; warning doesn't.</summary>
public record Finding(string Code, string Severity, string Path, string Message, JsonElement? Suggestion = null);

/// <summary>PUT /api/v1/slices/{slug}: the version stored (sha256 of the content) and its findings.</summary>
public record PutSliceResult(string Slug, string Version, bool Created, List<Finding> Findings);

/// <summary>POST /api/v1/slices/{slug}/validate: findings only, nothing stored.</summary>
public record ValidateResult(bool Valid, string Version, List<Finding> Findings);

/// <summary>POST /api/v1/instances. Prefix sets the instance ID and hostname prefix, so one enclave can run once per execution.</summary>
public record CreateInstanceRequest(string Slice, string Version = null, string Prefix = null);

public record SliceRef(string Slug, string Version);

public static class JobState
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class InstanceState
{
    public const string Pending = "pending";
    public const string Provisioning = "provisioning";
    public const string Configuring = "configuring";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string Destroying = "destroying";
    public const string Destroyed = "destroyed";
}

/// <summary>A build, teardown or plan, run one at a time. GET /api/v1/jobs/{id}. Stage and attempt say where it is.</summary>
public record JobRecord(
    string Id,
    string Kind,
    string Target,
    string State,
    List<string> Stages,
    DateTime Created,
    SliceRef Slice = null,
    string Stage = null,
    string Attempt = null,
    DateTime? Started = null,
    DateTime? Finished = null,
    string Error = null,
    string RetryOf = null);

/// <summary>What a create, destroy or retry returns: the job, and the instance it is for.</summary>
public record JobAccepted(JobRecord Job, string Instance = null);

public record ManifestSegment(string Name, string Cidr, string Gateway);

public record ManifestSite(string Name, string Router, string Isp, [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Location,
    string WanIp, string Edge, List<ManifestSegment> Segments);

/// <summary>
/// A host GraySpace built. Hostname is &lt;prefix&gt;-&lt;name&gt;, what the host (and a GHOSTS client on it) reports.
/// Labels are the spec's, such as ghosts.pool.
/// </summary>
public record ManifestHost(
    string Name,
    string Hostname,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Site,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string Segment,
    string Ip,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string PublicIp,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string MgmtIp,
    List<string> Fqdns,
    List<string> Roles,
    Dictionary<string, string> Labels,
    string Image,
    string Status);

public record ManifestRecord(string Name, string Type, string Data);

public record ManifestCertificate(string Host, List<string> Names, string Ca);

/// <summary>An instance and, once ready, its manifest. GET /api/v1/instances/{id}. The ID is its prefix.</summary>
public record InstanceView(
    string Id,
    SliceRef Slice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] BaseVersionRef Base,
    string State,
    List<ManifestSite> Sites,
    List<ManifestHost> Hosts,
    List<ManifestRecord> Dns,
    List<ManifestCertificate> Certificates,
    DateTime Created,
    DateTime Updated,
    List<string> StagesDone,
    List<string> Jobs,
    string Error = null);

/// <summary>GraySpace, as GHOSTS uses it. GraySpaceClient calls the HTTP API; GraySpaceSimulator answers in memory.</summary>
public interface IGraySpace
{
    Task<DeployedBase> GetBaseAsync(CancellationToken ct);
    Task<GraySpaceCapabilities> GetCapabilitiesAsync(CancellationToken ct);
    Task<List<SliceSummary>> ListSlicesAsync(CancellationToken ct);
    Task<PutSliceResult> PutSliceAsync(SliceSpec spec, CancellationToken ct);
    Task<ValidateResult> ValidateSliceAsync(SliceSpec spec, CancellationToken ct);
    Task<JobAccepted> CreateInstanceAsync(CreateInstanceRequest request, CancellationToken ct);
    Task<InstanceView> GetInstanceAsync(string instanceId, CancellationToken ct);
    Task<JobAccepted> DestroyInstanceAsync(string instanceId, CancellationToken ct);
    Task<JobRecord> GetJobAsync(string jobId, CancellationToken ct);

    /// <summary>The job's log so far, as text.</summary>
    Task<string> GetJobLogAsync(string jobId, CancellationToken ct);

    /// <summary>Runs a failed or cancelled job again, from the stage that failed.</summary>
    Task<JobAccepted> RetryJobAsync(string jobId, CancellationToken ct);

    /// <summary>The map layer as GeoJSON text, its enclave features limited to one instance when one is named.</summary>
    Task<string> GetMapAsync(string instanceId, CancellationToken ct);
}
