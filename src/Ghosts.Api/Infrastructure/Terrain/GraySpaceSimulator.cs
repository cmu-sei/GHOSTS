// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>
/// An in-memory GraySpace, for development without Proxmox: the Terrain API 1.3.0 calls GHOSTS makes, in GraySpace's
/// shapes and with its behaviour where GHOSTS depends on it. It builds nothing. A stored enclave is checked for the
/// findings GHOSTS is most likely to meet, and an instance of one with errors is refused (422 with the findings). Jobs
/// run one at a time, as behind GraySpace's node lock: a create is queued for at least two seconds and then runs its
/// stages until the build time passes; a destroy takes five seconds and cancels a create still running. An instance's
/// state changes when a job starts, not when it is asked for. Its manifest is allocated as GraySpace names one
/// (hostnames &lt;prefix&gt;-&lt;name&gt;). FailNext makes the next job of a kind fail at a stage. Tick publishes state
/// changes as /api/v1/events messages to every subscriber.
/// </summary>
public partial class GraySpaceSimulator(Func<DateTime> clock, int buildSeconds) : IGraySpace
{
    private const int QueuedSeconds = 2;
    private const int TeardownSeconds = 5;
    private static readonly string[] CreateStages = ["validate", "allocate", "render", "apply", "wait", "configure", "dns", "conditions", "check", "ready"];
    private static readonly string[] DestroyStages = ["revoke", "dns", "conditions", "detach", "destroy", "workspace", "images", "free"];

    public static readonly BaseVersionRef BaseRef = new("grayspace/2026.10", "simulated", "tier1");

    private sealed record Router(string Hostname, string Isp, int Asn, string Location, string CustomerNet, double Lat, double Lon);

    private static readonly Router[] Routers =
    [
        new("lum-chi", "Lumen", 3356, "Chicago", "209.151.130", 41.88, -87.63),
        new("are-ash", "Arelion", 1299, "Ashburn", "62.115.40", 39.04, -77.49),
        new("lum-ash", "Lumen", 3356, "Ashburn", "209.151.140", 39.02, -77.46)
    ];

    private static readonly GraySpaceImage[] Images =
    [
        new("alpine-3.22", "lxc", "http://download.proxmox.com/images/system/alpine-3.22-default_20250617_amd64.tar.xz", "alpine", "Alpine Linux 3.22"),
        new("ubuntu-22.04-lxc", "lxc", "http://download.proxmox.com/images/system/ubuntu-22.04-standard_22.04-1_amd64.tar.zst", "ubuntu", "Ubuntu 22.04 container"),
        new("ubuntu-24.04-vm", "vm", "https://cloud-images.ubuntu.com/noble/current/noble-server-cloudimg-amd64.img", "ubuntu", "Ubuntu 24.04 cloud image VM"),
        new("ghosts-pandora", "oci", "docker.io/dustinupdyke/ghosts-pandora:latest", "oci", "GHOSTS Pandora web and social media server", ["tls-kestrel"])
    ];

    private static readonly GraySpaceRole[] Roles =
    [
        new("web", "nginx with a placeholder page", ["ubuntu", "debian"]),
        new("ghosts-client", "A GHOSTS client installed from client_url, reporting to api_url", ["ubuntu", "debian", "windows"]),
        new("mail", "Postfix and Dovecot", ["ubuntu", "debian"]),
        new("dns", "BIND for <prefix>.internal", ["ubuntu", "debian"]),
        new("file", "A Samba share", ["ubuntu", "debian"]),
        new("ad-dc", "A Samba AD domain controller", ["ubuntu", "debian"])
    ];

    private static readonly Dictionary<string, string[]> RequiredRoleVars = new()
    {
        ["ghosts-client"] = ["client_url", "api_url"],
        ["ad-dc"] = ["realm", "domain", "admin_password"]
    };

    private sealed record StoredVersion(string Version, SliceSpec Spec, DateTime Created, List<Finding> Findings);

    private sealed class SimJob
    {
        public string Id, Kind, Target, Failure, FailStage, RetryOf;
        public SliceRef Slice;
        public DateTime Created, Starts, Ends;
        public DateTime? CancelledAt;
    }

    private sealed class SimInstance
    {
        public string Id, CreateJob, DestroyJob;
        public bool Retried;
        public SliceRef Slice;
        public DateTime Created;
        public List<ManifestSite> Sites = [];
        public List<ManifestHost> Hosts = [];
        public List<string> Jobs = [];
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, List<StoredVersion>> _slices = new();
    private readonly Dictionary<string, SimInstance> _instances = new();
    private readonly Dictionary<string, SimJob> _jobs = new();
    private readonly Dictionary<string, string> _published = new();
    private readonly List<Channel<string>> _subscribers = [];
    private readonly Dictionary<string, (string Stage, string Error)> _failNext = new();
    private DateTime _busyUntil = DateTime.MinValue;
    private int _jobSeq;
    private int _instanceSeq;

    /// <summary>The next job of this kind (create_instance or destroy_instance) fails when it reaches the stage.</summary>
    public void FailNext(string kind, string stage, string error)
    {
        lock (_lock) _failNext[kind] = (stage, error);
    }

    public Task<DeployedBase> GetBaseAsync(CancellationToken ct) =>
        Task.FromResult(new DeployedBase(BaseRef, "healthy", new RouterHealth(Routers.Length, Routers.Length, []), new BgpHealth(4, 4)));

    public Task<GraySpaceCapabilities> GetCapabilitiesAsync(CancellationToken ct) => Task.FromResult(new GraySpaceCapabilities(
        BaseRef, [.. Images], [.. Roles], [], ["nat-site", "published-host", "internet-host"], true, "10.0.0.0/16", "192.168.0.0/16",
        new DnsCapabilities(["com", "org", "net"], [new DnsServer("dns-cloudflare", "198.41.0.2", "are-ash", "Arelion")],
            [new DnsServer("dns-resolver-chi", "209.151.130.5", "lum-chi", "Lumen")]),
        [new CertificateAuthority("ca-graytrust", "GrayTrust", "209.151.140.6", "lum-ash", "Lumen")],
        Routers.GroupBy(r => r.Isp).Select(g => new IspCapability(g.Key, g.First().Asn, g.Select(r => r.Location).Distinct().ToList())).ToList(),
        null));

    public Task<List<SliceSummary>> ListSlicesAsync(CancellationToken ct)
    {
        lock (_lock)
        {
            var list = new List<SliceSummary> { new("base", "base", "The deployed base", Latest: BaseRef.Name) };
            list.AddRange(_slices.Select(s => new SliceSummary(s.Key, "slice", s.Value[^1].Spec.Description ?? string.Empty, s.Value[^1].Spec.Owner,
                s.Value[^1].Version, s.Value.Select(v => new SliceVersion(v.Version, v.Created)).ToList(),
                _instances.Values.Where(i => i.Slice.Slug == s.Key && InstanceStateOf(i) != InstanceState.Destroyed).Select(i => i.Id).ToList())));
            return Task.FromResult(list);
        }
    }

    public Task<PutSliceResult> PutSliceAsync(SliceSpec spec, CancellationToken ct)
    {
        Structural(spec);
        var findings = Validate(spec);
        var version = Version(spec);
        lock (_lock)
        {
            var versions = _slices.TryGetValue(spec.Slug, out var v) ? v : _slices[spec.Slug] = [];
            var created = versions.Count == 0 || versions[^1].Version != version;
            if (created) versions.Add(new StoredVersion(version, spec, clock(), findings));
            return Task.FromResult(new PutSliceResult(spec.Slug, version, created, findings));
        }
    }

    public Task<ValidateResult> ValidateSliceAsync(SliceSpec spec, CancellationToken ct)
    {
        Structural(spec);
        var findings = Validate(spec);
        return Task.FromResult(new ValidateResult(findings.All(f => f.Severity != "error"), Version(spec), findings));
    }

    public Task<JobAccepted> CreateInstanceAsync(CreateInstanceRequest request, CancellationToken ct)
    {
        lock (_lock)
        {
            if (!_slices.TryGetValue(request.Slice ?? string.Empty, out var versions))
                throw Error(HttpStatusCode.NotFound, $"No slice '{request.Slice}'");
            var stored = request.Version == null ? versions[^1] : versions.FirstOrDefault(v => v.Version == request.Version)
                ?? throw Error(HttpStatusCode.NotFound, $"Slice '{request.Slice}' has no version '{request.Version}'");
            var id = request.Prefix ?? stored.Spec.Prefix;
            if (!PrefixPattern().IsMatch(id ?? string.Empty))
                throw Error(HttpStatusCode.UnprocessableEntity, $"Prefix '{id}' must be a lowercase letter and up to 7 more letters or digits");
            if (_instances.TryGetValue(id, out var existing) && InstanceStateOf(existing) != InstanceState.Destroyed)
                throw Error(HttpStatusCode.Conflict, $"Instance '{id}' exists ({InstanceStateOf(existing)}); destroy it first");

            var errors = stored.Findings.Where(f => f.Severity == "error").ToList();
            if (errors.Count > 0)
                throw new TerrainProviderException(422, "invalid_request", "The slice can't be instantiated") { Findings = errors };

            var instance = new SimInstance { Id = id, Slice = new SliceRef(request.Slice, stored.Version), Created = clock() };
            Allocate(instance, stored.Spec, ++_instanceSeq);
            var job = NewCreateJob(instance, stored.Findings, retryOf: null);
            _instances[id] = instance;
            return Task.FromResult(new JobAccepted(Describe(job), id));
        }
    }

    public Task<InstanceView> GetInstanceAsync(string instanceId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(View(Instance(instanceId)));
    }

    public Task<JobAccepted> DestroyInstanceAsync(string instanceId, CancellationToken ct)
    {
        lock (_lock)
        {
            var instance = Instance(instanceId);
            if (InstanceStateOf(instance) == InstanceState.Destroyed)
                throw Error(HttpStatusCode.Conflict, $"Instance '{instanceId}' is already destroyed");
            if (instance.DestroyJob != null && JobStateOf(_jobs[instance.DestroyJob]) is JobState.Queued or JobState.Running)
                throw Error(HttpStatusCode.Conflict, $"Instance '{instanceId}' is already being destroyed");

            var now = clock();
            var create = _jobs[instance.CreateJob];
            if (JobStateOf(create) is JobState.Queued or JobState.Running)
            {
                // GraySpace cancels a create still queued or running; the runner is free from then on.
                if (_busyUntil == create.Ends) _busyUntil = now;
                create.CancelledAt = now;
            }

            var job = Schedule(new SimJob { Id = $"j-{++_jobSeq}", Kind = "destroy_instance", Target = instance.Id, Slice = instance.Slice, Created = now },
                now, TeardownSeconds, DestroyStages);
            _jobs[job.Id] = job;
            instance.DestroyJob = job.Id;
            instance.Jobs.Add(job.Id);
            return Task.FromResult(new JobAccepted(Describe(job), instance.Id));
        }
    }

    public Task<JobRecord> GetJobAsync(string jobId, CancellationToken ct)
    {
        lock (_lock) return Task.FromResult(Describe(Job(jobId)));
    }

    public Task<string> GetJobLogAsync(string jobId, CancellationToken ct)
    {
        lock (_lock)
        {
            var job = Job(jobId);
            var record = Describe(job);
            var stages = Stages(job);
            var log = new StringBuilder($"{job.Created:O} {job.Kind} {job.Target} queued\n");
            var reached = record.State == JobState.Succeeded ? stages.Length : Math.Max(0, Array.IndexOf(stages, record.Stage) + (record.State == JobState.Failed ? 1 : 0));
            for (var i = 0; i < reached && record.State != JobState.Queued; i++) log.Append($"stage {stages[i]}\n");
            if (record.State == JobState.Running) log.Append($"stage {record.Stage}\n");
            if (record.State is JobState.Succeeded or JobState.Failed or JobState.Cancelled) log.Append($"{record.State}{(record.Error != null ? $": {record.Error}" : "")}\n");
            return Task.FromResult(log.ToString());
        }
    }

    public Task<JobAccepted> RetryJobAsync(string jobId, CancellationToken ct)
    {
        lock (_lock)
        {
            var old = Job(jobId);
            var state = JobStateOf(old);
            if (state is not (JobState.Failed or JobState.Cancelled))
                throw Error(HttpStatusCode.Conflict, $"Job {jobId} is {state}; only failed or cancelled jobs can be retried");
            var instance = Instance(old.Target);
            if (old.Kind != "create_instance")
                throw Error(HttpStatusCode.Conflict, $"The simulator retries create jobs only, not {old.Kind}");
            var stored = _slices[instance.Slice.Slug].First(v => v.Version == instance.Slice.Version);
            var job = NewCreateJob(instance, Validate(stored.Spec), retryOf: old.Id);
            return Task.FromResult(new JobAccepted(Describe(job), instance.Id));
        }
    }

    public Task<string> GetMapAsync(string instanceId, CancellationToken ct)
    {
        lock (_lock)
        {
            var features = new JsonArray();
            foreach (var r in Routers)
                features.Add(Feature(r.Hostname, Point(r.Lon, r.Lat), new JsonObject
                {
                    ["id"] = r.Hostname, ["kind"] = "router", ["isp"] = r.Isp, ["asn"] = r.Asn, ["location"] = r.Location,
                    ["color"] = r.Isp == "Lumen" ? "#1f77b4" : "#ff7f0e", ["status"] = "running"
                }));
            foreach (var (a, b) in new[] { ("lum-chi", "are-ash"), ("lum-chi", "lum-ash") })
            {
                var ra = Routers.Single(r => r.Hostname == a);
                var rb = Routers.Single(r => r.Hostname == b);
                features.Add(Feature($"{a}:{b}", Line((ra.Lon, ra.Lat), (rb.Lon, rb.Lat)), new JsonObject
                {
                    ["id"] = $"{a}:{b}", ["kind"] = "link", ["type"] = ra.Isp == rb.Isp ? "INTERNAL_LINK" : "PEERS_WITH", ["state"] = "up", ["color"] = "#2ca02c"
                }));
            }
            foreach (var instance in _instances.Values.Where(i => InstanceStateOf(i) == InstanceState.Ready && (string.IsNullOrEmpty(instanceId) || i.Id == instanceId)))
            foreach (var site in instance.Sites)
            {
                var r = Routers.Single(x => x.Hostname == site.Router);
                var (lon, lat) = (r.Lon + 0.15, r.Lat + 0.1);
                features.Add(Feature($"{instance.Id}/{site.Name}", Point(lon, lat), new JsonObject
                {
                    ["id"] = $"{instance.Id}/{site.Name}", ["kind"] = "site", ["instance"] = instance.Id, ["router"] = site.Router, ["wan_ip"] = site.WanIp, ["state"] = "ready"
                }));
                features.Add(Feature($"{instance.Id}/{site.Name}/attachment", Line((lon, lat), (r.Lon, r.Lat)), new JsonObject
                {
                    ["id"] = $"{instance.Id}/{site.Name}/attachment", ["kind"] = "attachment", ["instance"] = instance.Id
                }));
            }
            return Task.FromResult(new JsonObject { ["type"] = "FeatureCollection", ["features"] = features }.ToJsonString());
        }
    }

    /// <summary>A reader of /api/v1/events messages, as JSON text, from now on.</summary>
    public ChannelReader<string> Subscribe()
    {
        var channel = Channel.CreateUnbounded<string>();
        lock (_lock) _subscribers.Add(channel);
        return channel.Reader;
    }

    public void Unsubscribe(ChannelReader<string> reader)
    {
        lock (_lock) _subscribers.RemoveAll(c => c.Reader == reader);
    }

    /// <summary>Publishes job.update, instance.update and feature.update for every state that changed since the last tick.</summary>
    public void Tick()
    {
        lock (_lock)
        {
            var now = clock().ToString("O");
            foreach (var job in _jobs.Values)
            {
                var r = Describe(job);
                if (!Changed($"job:{job.Id}", $"{r.State}/{r.Stage}")) continue;
                Publish(new JsonObject
                {
                    ["type"] = "job.update", ["id"] = r.Id, ["kind"] = r.Kind, ["target"] = r.Target, ["state"] = r.State,
                    ["stage"] = r.Stage, ["attempt"] = r.Attempt, ["error"] = r.Error, ["timestamp"] = now
                });
            }
            foreach (var instance in _instances.Values)
            {
                var state = InstanceStateOf(instance);
                if (!Changed($"instance:{instance.Id}", state)) continue;
                Publish(new JsonObject { ["type"] = "instance.update", ["id"] = instance.Id, ["state"] = state, ["error"] = View(instance).Error, ["timestamp"] = now });
                if (state is InstanceState.Ready or InstanceState.Destroyed)
                    foreach (var site in instance.Sites)
                        Publish(new JsonObject
                        {
                            ["type"] = "feature.update", ["id"] = $"{instance.Id}/{site.Name}", ["kind"] = "site",
                            ["properties"] = new JsonObject { ["state"] = state }, ["timestamp"] = now
                        });
            }
        }
    }

    // ── validation ───────────────────────────────────────────────────────

    private static List<Finding> Validate(SliceSpec spec)
    {
        var findings = new List<Finding>();
        void Add(string code, string path, string message) => findings.Add(new Finding(code, "error", path, message));

        if (!SlugPattern().IsMatch(spec.Slug ?? string.Empty) || spec.Slug == "base") Add("INVALID_SPEC", "slug", $"Slug '{spec.Slug}' must be lowercase letters, digits and hyphens, and not 'base'");
        if (!PrefixPattern().IsMatch(spec.Prefix ?? string.Empty)) Add("INVALID_SPEC", "prefix", $"Prefix '{spec.Prefix}' must be a lowercase letter and up to 7 more letters or digits");

        var segments = new HashSet<string>();
        var siteNames = new HashSet<string>();
        for (var i = 0; i < (spec.Sites ?? []).Count; i++)
        {
            var site = spec.Sites[i];
            if (!siteNames.Add(site.Name)) Add("DUPLICATE_NAME", $"sites[{i}].name", $"Site '{site.Name}' is defined twice");
            if (site.Attach?.Router is { } router && Routers.All(r => r.Hostname != router))
                Add("UNKNOWN_ROUTER", $"sites[{i}].attach.router", $"No router '{router}' in the deployed base");
            for (var j = 0; j < (site.Segments ?? []).Count; j++)
            {
                var segment = site.Segments[j];
                if (!segments.Add(segment.Name)) Add("DUPLICATE_NAME", $"sites[{i}].segments[{j}].name", $"Segment '{segment.Name}' is defined twice");
                if (segment.Cidr == null) continue;
                if (!IPNetwork.TryParse(segment.Cidr, out var net) || !IsPrivate(net) || net.PrefixLength > 28)
                    Add("INVALID_CIDR", $"sites[{i}].segments[{j}].cidr", $"{segment.Cidr} is not an RFC 1918 range of /28 or larger");
                else if (Overlaps(net, IPNetwork.Parse("10.0.0.0/16")))
                    Add("CIDR_OVERLAPS_MGMT", $"sites[{i}].segments[{j}].cidr", $"{segment.Cidr} overlaps the management subnet 10.0.0.0/16");
            }
        }

        var hostNames = new HashSet<string>();
        var usedRoles = new HashSet<string>();
        for (var i = 0; i < (spec.Hosts ?? []).Count; i++)
        {
            var host = spec.Hosts[i];
            if (!hostNames.Add(host.Name)) Add("DUPLICATE_NAME", $"hosts[{i}].name", $"Host '{host.Name}' is defined twice");
            if (host.Segment != null && !segments.Contains(host.Segment)) Add("UNKNOWN_SEGMENT", $"hosts[{i}].segment", $"No segment '{host.Segment}' in the spec");
            if (host.Segment == null && host.Internet == null) Add("INVALID_SPEC", $"hosts[{i}]", $"Host '{host.Name}' needs a segment or an internet placement");
            var image = Images.FirstOrDefault(m => m.Name == host.Image);
            if (image == null) Add("UNKNOWN_IMAGE", $"hosts[{i}].image", $"No image '{host.Image}' in the catalog");
            for (var j = 0; j < (host.Roles ?? []).Count; j++)
            {
                var role = Roles.FirstOrDefault(r => r.Name == host.Roles[j]);
                if (role == null) { Add("UNKNOWN_ROLE", $"hosts[{i}].roles[{j}]", $"No role '{host.Roles[j]}' in the catalog"); continue; }
                usedRoles.Add(role.Name);
                if (image == null) continue;
                if (image.Type == "oci") Add("INCOMPATIBLE_ROLE", $"hosts[{i}].roles[{j}]", $"OCI image {image.Name} takes no roles");
                else if (!role.OsFamilies.Contains(image.OsFamily)) Add("INCOMPATIBLE_ROLE", $"hosts[{i}].roles[{j}]", $"Role {role.Name} doesn't support {image.OsFamily}");
            }
        }

        foreach (var role in usedRoles.Where(RequiredRoleVars.ContainsKey))
        foreach (var variable in RequiredRoleVars[role])
            if (spec.RoleVars?.GetValueOrDefault(role)?.GetValueOrDefault(variable) is not { Length: > 0 })
                Add("MISSING_ROLE_VAR", $"role_vars.{role}.{variable}", $"Role {role} needs role_vars.{role}.{variable}");

        return findings;
    }

    private static bool IsPrivate(IPNetwork net) =>
        new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" }.Any(p => IPNetwork.Parse(p) is var block && block.Contains(net.BaseAddress) && net.PrefixLength >= block.PrefixLength);

    private static bool Overlaps(IPNetwork a, IPNetwork b) => a.Contains(b.BaseAddress) || b.Contains(a.BaseAddress);

    private static string Version(SliceSpec spec) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(spec, GraySpaceClient.Wire))));

    // ── allocation ───────────────────────────────────────────────────────

    private static void Allocate(SimInstance instance, SliceSpec spec, int number)
    {
        var statics = new Dictionary<string, int>();
        string TakeStatic(Router r) => $"{r.CustomerNet}.{2 + (statics[r.Hostname] = statics.GetValueOrDefault(r.Hostname) + 1) - 1}";
        Router Pick(SliceAttach a) =>
            Routers.FirstOrDefault(r => r.Hostname == a?.Router)
            ?? Routers.FirstOrDefault(r => (a?.Isp == null || r.Isp == a.Isp) && (a?.Location == null || r.Location == a.Location))
            ?? Routers[0];

        var segmentNets = new Dictionary<string, (string Site, string Prefix, int Next)>();
        var pool = 10;
        foreach (var site in spec.Sites ?? [])
        {
            var router = Pick(site.Attach);
            var segments = new List<ManifestSegment>();
            foreach (var segment in site.Segments ?? [])
            {
                var cidr = segment.Cidr ?? $"192.168.{pool++}.0/24";
                var prefix = string.Join('.', cidr.Split('/')[0].Split('.').Take(3));
                segments.Add(new ManifestSegment(segment.Name, cidr, $"{prefix}.1"));
                segmentNets[segment.Name] = (site.Name, prefix, 10);
            }
            instance.Sites.Add(new ManifestSite(site.Name, router.Hostname, router.Isp, router.Location, TakeStatic(router), $"{instance.Id}-{site.Name}-edge", segments));
        }

        var mgmt = 10;
        foreach (var host in spec.Hosts ?? [])
        {
            var width = Math.Max(2, (host.Count ?? 0).ToString().Length);
            var names = host.Count is > 0 ? Enumerable.Range(1, host.Count.Value).Select(n => $"{host.Name}-{n.ToString().PadLeft(width, '0')}") : [host.Name];
            foreach (var name in names)
            {
                string site = null, segment = null, ip, publicIp = null;
                if (host.Segment != null && segmentNets.TryGetValue(host.Segment, out var net))
                {
                    (site, segment) = (net.Site, host.Segment);
                    ip = $"{net.Prefix}.{net.Next}";
                    segmentNets[host.Segment] = net with { Next = net.Next + 1 };
                    if (host.Publish == true) publicIp = TakeStatic(Pick(spec.Sites.First(s => s.Name == site).Attach));
                }
                else ip = TakeStatic(Pick(host.Internet));
                var image = Images.FirstOrDefault(m => m.Name == host.Image);
                instance.Hosts.Add(new ManifestHost(name, $"{instance.Id}-{name}", site, segment, ip, publicIp,
                    image?.Type == "oci" ? null : $"10.0.{number}.{mgmt++}", [], [.. host.Roles ?? []],
                    new Dictionary<string, string>(host.Labels ?? []), host.Image, "running"));
            }
        }
    }

    // ── jobs and instances ───────────────────────────────────────────────

    private SimJob NewCreateJob(SimInstance instance, List<Finding> findings, string retryOf)
    {
        var now = clock();
        var job = new SimJob { Id = $"j-{++_jobSeq}", Kind = "create_instance", Target = instance.Id, Slice = instance.Slice, Created = now, RetryOf = retryOf };
        var errors = findings.Where(f => f.Severity == "error").ToList();
        if (errors.Count > 0) _failNext["create_instance"] = ("validate", string.Join("; ", errors.Select(f => $"{f.Code} at {f.Path}: {f.Message}")));
        Schedule(job, now.AddSeconds(QueuedSeconds), Math.Max(1, buildSeconds - QueuedSeconds), CreateStages);
        _jobs[job.Id] = job;
        instance.CreateJob = job.Id;
        instance.DestroyJob = null;
        instance.Retried = retryOf != null;
        instance.Jobs.Add(job.Id);
        return job;
    }

    /// <summary>Puts a job in the runner's queue: it starts once the runner is free, and ends early at a stage FailNext named.</summary>
    private SimJob Schedule(SimJob job, DateTime earliest, int seconds, string[] stages)
    {
        job.Starts = earliest > _busyUntil ? earliest : _busyUntil;
        job.Ends = job.Starts.AddSeconds(seconds);
        if (_failNext.Remove(job.Kind, out var failure))
        {
            var index = Math.Max(0, Array.IndexOf(stages, failure.Stage));
            job.Ends = job.Starts.AddSeconds(seconds * (double)index / stages.Length);
            (job.FailStage, job.Failure) = (stages[index], $"{stages[index]}: {failure.Error}");
        }
        _busyUntil = job.Ends;
        return job;
    }

    /// <summary>What GraySpace's request validation refuses before any finding: here, an attach that names nothing.</summary>
    private static void Structural(SliceSpec spec)
    {
        bool Empty(SliceAttach a) => a == null || (string.IsNullOrEmpty(a.Isp) && string.IsNullOrEmpty(a.Location) && string.IsNullOrEmpty(a.Router));
        for (var i = 0; i < (spec.Sites ?? []).Count; i++)
            if (Empty(spec.Sites[i].Attach))
                throw new TerrainProviderException(422, "invalid_request", $"body.sites.{i}.attach: Value error, give an isp, a location, or a router");
        for (var i = 0; i < (spec.Hosts ?? []).Count; i++)
            if (spec.Hosts[i].Internet != null && Empty(spec.Hosts[i].Internet))
                throw new TerrainProviderException(422, "invalid_request", $"body.hosts.{i}.internet: Value error, give an isp, a location, or a router");
    }

    private static string[] Stages(SimJob job) => job.Kind == "destroy_instance" ? DestroyStages : CreateStages;

    private string JobStateOf(SimJob job) => Describe(job).State;

    private JobRecord Describe(SimJob job)
    {
        var now = clock();
        var stages = Stages(job);
        if (job.CancelledAt is { } cancelled && now >= cancelled)
            return new JobRecord(job.Id, job.Kind, job.Target, JobState.Cancelled, [.. stages], job.Created, job.Slice, null, null,
                cancelled > job.Starts ? job.Starts : null, cancelled, "Cancelled", job.RetryOf);
        if (job.Failure != null && now >= job.Ends)
            return new JobRecord(job.Id, job.Kind, job.Target, JobState.Failed, [.. stages], job.Created, job.Slice, job.FailStage, null, job.Starts, job.Ends, job.Failure, job.RetryOf);
        if (now < job.Starts)
            return new JobRecord(job.Id, job.Kind, job.Target, JobState.Queued, [.. stages], job.Created, job.Slice, RetryOf: job.RetryOf);
        if (now < job.Ends)
        {
            var full = job.Failure == null ? (job.Ends - job.Starts).TotalSeconds
                : (job.Ends - job.Starts).TotalSeconds * stages.Length / Math.Max(1, Array.IndexOf(stages, job.FailStage));
            var fraction = (now - job.Starts).TotalSeconds / full;
            var stage = stages[Math.Min(stages.Length - 1, (int)(fraction * stages.Length))];
            return new JobRecord(job.Id, job.Kind, job.Target, JobState.Running, [.. stages], job.Created, job.Slice, stage, stage == "apply" ? "1/6" : null, job.Starts, null, null, job.RetryOf);
        }
        return new JobRecord(job.Id, job.Kind, job.Target, JobState.Succeeded, [.. stages], job.Created, job.Slice, null, null, job.Starts, job.Ends, null, job.RetryOf);
    }

    /// <summary>As GraySpace sets it: when a job starts or ends, not when it is asked for.</summary>
    private string InstanceStateOf(SimInstance instance)
    {
        if (instance.DestroyJob != null)
        {
            switch (JobStateOf(_jobs[instance.DestroyJob]))
            {
                case JobState.Succeeded: return InstanceState.Destroyed;
                case JobState.Running: return InstanceState.Destroying;
                case JobState.Failed or JobState.Cancelled: return InstanceState.Failed;
            }
        }
        var job = Describe(_jobs[instance.CreateJob]);
        return job.State switch
        {
            JobState.Queued => instance.Retried ? InstanceState.Failed : InstanceState.Pending,
            JobState.Running => Array.IndexOf(CreateStages, job.Stage) < Array.IndexOf(CreateStages, "wait") ? InstanceState.Provisioning : InstanceState.Configuring,
            JobState.Succeeded => InstanceState.Ready,
            _ => InstanceState.Failed
        };
    }

    private InstanceView View(SimInstance instance)
    {
        var state = InstanceStateOf(instance);
        var create = Describe(_jobs[instance.CreateJob]);
        var ready = state == InstanceState.Ready;
        var done = create.State == JobState.Succeeded ? CreateStages.ToList()
            : CreateStages.Take(Math.Max(0, Array.IndexOf(CreateStages, create.Stage))).ToList();
        return new InstanceView(instance.Id, instance.Slice, BaseRef, state,
            ready ? instance.Sites : [], ready ? instance.Hosts : [], [], [], instance.Created, clock(), done, [.. instance.Jobs],
            state == InstanceState.Failed ? create.Error : null);
    }

    private SimInstance Instance(string id) =>
        _instances.TryGetValue(id ?? string.Empty, out var instance) ? instance : throw Error(HttpStatusCode.NotFound, $"No instance '{id}'");

    private SimJob Job(string id) =>
        _jobs.TryGetValue(id ?? string.Empty, out var job) ? job : throw Error(HttpStatusCode.NotFound, $"No job '{id}'");

    private bool Changed(string key, string value)
    {
        if (_published.TryGetValue(key, out var last) && last == value) return false;
        _published[key] = value;
        return true;
    }

    private void Publish(JsonObject message)
    {
        var text = message.ToJsonString();
        foreach (var subscriber in _subscribers) subscriber.Writer.TryWrite(text);
    }

    private static JsonObject Feature(string id, JsonObject geometry, JsonObject properties) =>
        new() { ["type"] = "Feature", ["id"] = id, ["geometry"] = geometry, ["properties"] = properties };

    private static JsonObject Point(double lon, double lat) => new() { ["type"] = "Point", ["coordinates"] = new JsonArray(lon, lat) };

    private static JsonObject Line((double Lon, double Lat) a, (double Lon, double Lat) b) =>
        new() { ["type"] = "LineString", ["coordinates"] = new JsonArray(new JsonArray(a.Lon, a.Lat), new JsonArray(b.Lon, b.Lat)) };

    private static TerrainProviderException Error(HttpStatusCode status, string detail) => new((int)status, status switch
    {
        HttpStatusCode.NotFound => "not_found",
        HttpStatusCode.Conflict => "conflict",
        _ => "invalid_request"
    }, detail);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z][a-z0-9]{0,7}$")]
    private static partial Regex PrefixPattern();
}
