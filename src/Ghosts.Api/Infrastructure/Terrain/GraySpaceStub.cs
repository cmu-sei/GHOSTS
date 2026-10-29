// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ghosts.Api.Infrastructure.Terrain;

public record StubManifestResult(TerrainManifest Manifest, TerrainError Error);

/// <summary>
/// An in-memory GraySpace, for development and for showing the contract. It builds nothing: an instance is
/// queued for two seconds, building until its build time passes, then ready, and its manifest is made from the
/// hosts the request expected (or a small default enclave). Teardown takes five seconds.
/// </summary>
public class GraySpaceStub(Func<DateTime> clock, int buildSeconds)
{
    private const int QueuedSeconds = 2;
    private const int TeardownSeconds = 5;
    private const string BaseId = "office-base";

    private readonly ConcurrentDictionary<string, Entry> _instances = new();
    private readonly ConcurrentDictionary<string, string> _byKey = new();
    private int _nextVmid = 1000;

    private sealed class Entry
    {
        public string Id;
        public CreateTerrainInstance Request;
        public DateTime CreatedAt;
        public DateTime? DeletedAt;
        public List<TerrainManifestHost> Hosts;
    }

    private static readonly TerrainBaseSnapshot BaseSnapshot = new(BaseId, "2",
        [new TerrainSegment("Base internet", "198.51.100.0/24", "ISPs, DNS and the Pandora sites")],
        [
            new TerrainHost("dns-01", "Base internet", "linux", "dns", ["dns"]),
            new TerrainHost("mail-01", "Base internet", "linux", "mail", ["smtp", "imap"]),
            new TerrainHost("chirper", "Base internet", "linux", "pandora site", ["http"])
        ]);

    private static readonly TerrainExpectation DefaultEnclave = new(
        [new TerrainSegment("Office LAN", "10.20.0.0/24")],
        [
            new TerrainHost("desk-01", "Office LAN", "ubuntu", "workstation"),
            new TerrainHost("desk-02", "Office LAN", "ubuntu", "workstation"),
            new TerrainHost("desk-03", "Office LAN", "windows", "workstation"),
            new TerrainHost("file-01", "Office LAN", "ubuntu", "file server"),
            new TerrainHost("console-01", "Office LAN", "windows", "participant console")
        ]);

    public List<TerrainBase> ListBases() => [new TerrainBase(BaseId, "Office base", "2", "pve-01", "running")];

    public TerrainBaseSnapshot Snapshot(string baseId) => baseId == BaseId ? BaseSnapshot : null;

    public List<TerrainEnclave> ListEnclaves() => [new TerrainEnclave("office-enclave", "1", BaseId, DefaultEnclave.Hosts.Count)];

    public TerrainInstance Create(CreateTerrainInstance request)
    {
        if (!string.IsNullOrEmpty(request.IdempotencyKey) && _byKey.TryGetValue(request.IdempotencyKey, out var existing))
            return Get(existing);

        var id = $"inst-{Guid.NewGuid():N}"[..13];
        var expected = request.Expected?.Hosts?.Count > 0 ? request.Expected : DefaultEnclave;
        var entry = new Entry { Id = id, Request = request, CreatedAt = clock(), Hosts = ManifestHosts(expected) };
        _instances[id] = entry;
        if (!string.IsNullOrEmpty(request.IdempotencyKey)) _byKey[request.IdempotencyKey] = id;
        return Describe(entry);
    }

    public TerrainInstance Get(string id) => _instances.TryGetValue(id ?? string.Empty, out var entry) ? Describe(entry) : null;

    public StubManifestResult Manifest(string id)
    {
        if (!_instances.TryGetValue(id ?? string.Empty, out var entry))
            return new StubManifestResult(null, new TerrainError("instance_not_found", $"No instance {id}"));
        var status = Describe(entry).Status;
        if (status != TerrainStatus.Ready)
            return new StubManifestResult(null, new TerrainError("not_ready", $"Instance {id} is {status}"));
        return new StubManifestResult(new TerrainManifest(entry.Id, entry.Request.BaseId, entry.Request.Enclave, "office.local",
            entry.Hosts,
            [
                new TerrainSite("Chirper", "pandora", "http://chirper.office.local"),
                new TerrainSite("Mail", "mail", "smtp://mail-01.office.local"),
                new TerrainSite("Chat", "chat", "xmpp://chat.office.local")
            ]), null);
    }

    public TerrainInstance Delete(string id)
    {
        if (!_instances.TryGetValue(id ?? string.Empty, out var entry)) return null;
        entry.DeletedAt ??= clock();
        return Describe(entry);
    }

    private TerrainInstance Describe(Entry entry)
    {
        var now = clock();
        var age = (now - entry.CreatedAt).TotalSeconds;
        string status;
        int progress;
        if (entry.DeletedAt is { } deletedAt)
        {
            status = (now - deletedAt).TotalSeconds >= TeardownSeconds ? TerrainStatus.Deleted : TerrainStatus.TearingDown;
            progress = 0;
        }
        else if (age < QueuedSeconds) { status = TerrainStatus.Queued; progress = 0; }
        else if (age < buildSeconds) { status = TerrainStatus.Building; progress = (int)(age / buildSeconds * 100); }
        else { status = TerrainStatus.Ready; progress = 100; }

        var log = new List<string> { $"queued {entry.Request.Enclave} on {entry.Request.BaseId}" };
        if (age >= QueuedSeconds) log.Add($"building {entry.Hosts.Count} hosts");
        if (status == TerrainStatus.Ready) log.Add("ready");
        if (entry.DeletedAt != null) log.Add(status == TerrainStatus.Deleted ? "deleted" : "tearing down");

        return new TerrainInstance(entry.Id, entry.Request.BaseId, entry.Request.Enclave, status, progress,
            $"job-{entry.Id[5..]}", log, entry.CreatedAt, now);
    }

    private List<TerrainManifestHost> ManifestHosts(TerrainExpectation expected)
    {
        var segments = (expected.Segments ?? []).Select(s => s.Name).ToList();
        return expected.Hosts.Select((h, i) =>
        {
            var segmentIndex = Math.Max(0, segments.IndexOf(h.Segment));
            var participant = (h.Role ?? string.Empty).Contains("participant", StringComparison.OrdinalIgnoreCase)
                              || (h.Role ?? string.Empty).Contains("console", StringComparison.OrdinalIgnoreCase);
            var windows = (h.Os ?? string.Empty).Contains("windows", StringComparison.OrdinalIgnoreCase);
            return new TerrainManifestHost(h.Name, h.Segment, $"10.20.{segmentIndex}.{10 + i}", $"172.30.0.{10 + i}",
                windows ? "windows" : "linux", h.Os ?? (windows ? "windows" : "ubuntu"), h.Role ?? "workstation",
                "pve-01", System.Threading.Interlocked.Increment(ref _nextVmid), !participant, participant);
        }).ToList();
    }
}
