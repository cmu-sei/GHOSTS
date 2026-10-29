// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ghosts.Api.Infrastructure.Models;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>A pool of NPCs and where its people sit: segments, or named hosts.</summary>
public record PoolPlacement(string Role, int Count, List<string> Segments, List<string> Hosts);

/// <summary>What GHOSTS knows of an enclave: its name, the scenario's name and description, its terrain and its pools.</summary>
public record EnclaveInput(string Slug, string ScenarioName, string Description, TerrainExtrasDto Terrain, List<PoolPlacement> Pools);

/// <summary>The slice spec for an enclave, and what the mapping could not carry over as asked.</summary>
public record EnclaveMapping(SliceSpec Spec, List<string> Notes);

/// <summary>
/// Turns a scenario's terrain and population into the slice spec GraySpace builds (GraySpace's ghosts-integration-v2
/// §6.3). The terrain's segments make one NAT site attached to an ISP GraySpace has, where GraySpace picks the router;
/// hosts get a GraySpace image from
/// their os and roles from their role and services; each pool is GHOSTS-client hosts labelled ghosts.pool, either the
/// hosts it is placed on or counted hosts on its segments; a participant console is labelled ghosts.console and runs no
/// client. The client role's variables come from TerrainProvider settings.
/// </summary>
public static partial class EnclaveSliceMapper
{
    public const string ClientRole = "ghosts-client";
    public const string PoolLabel = "ghosts.pool";
    public const string ConsoleLabel = "ghosts.console";
    private const string SiteName = "main";

    /// <summary>GHOSTS words in a host's role or services, and the GraySpace role each means.</summary>
    private static readonly (string Role, string[] Words)[] RoleWords =
    [
        ("web", ["web", "http", "https", "www", "nginx", "intranet"]),
        ("mail", ["mail", "smtp", "imap", "pop3", "exchange"]),
        ("dns", ["dns", "bind", "name server"]),
        ("file", ["file", "smb", "samba", "share", "nas"])
    ];

    /// <summary>GHOSTS words for a role GraySpace has but whose required variables the document can't supply.</summary>
    private static readonly (string Role, string[] Words)[] UnconfigurableRoleWords =
    [
        ("ad-dc", ["domain controller", "active directory", "ad-dc"])
    ];

    public static EnclaveMapping Map(EnclaveInput input, GraySpaceCapabilities caps, TerrainProviderOptions settings)
    {
        var notes = new List<string>();
        var terrain = input.Terrain ?? new TerrainExtrasDto();
        var slug = Name(input.Slug ?? input.ScenarioName ?? "enclave", 63);
        if (slug == "base") slug = "base-enclave";

        // Segments: one NAT site. A private CIDR is kept; any other is dropped so GraySpace allocates one.
        var segmentNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var segments = new List<SliceSegment>();
        foreach (var segment in terrain.Segments ?? [])
        {
            if (string.IsNullOrWhiteSpace(segment.Name)) continue;
            var name = Name(segment.Name, 24);
            segmentNames[segment.Name] = name;
            var cidr = segment.Cidr;
            if (cidr != null && !(IPNetwork.TryParse(cidr, out var net) && IsPrivate(net)))
            {
                notes.Add($"Segment {segment.Name}: {cidr} is not a private range, so GraySpace allocates one");
                cidr = null;
            }
            segments.Add(new SliceSegment(name, cidr));
        }
        if (segments.Count == 0) segments.Add(new SliceSegment("lan"));
        string SegmentOf(string documentName, string what)
        {
            if (documentName != null && segmentNames.TryGetValue(documentName, out var name)) return name;
            if (documentName != null) notes.Add($"{what}: no segment {documentName} in the terrain, so it goes on {segments[0].Name}");
            return segments[0].Name;
        }

        // Hosts the terrain names, with the pools placed on them.
        var placedPool = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pool in input.Pools ?? [])
        foreach (var host in pool.Hosts ?? [])
            placedPool.TryAdd(host, pool.Role);

        var serviceWords = (terrain.Services ?? []).SelectMany(s => (s.Hosts ?? []).Select(h => (Host: h, Words: $"{s.Name} {s.Description}")))
            .GroupBy(x => x.Host, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => string.Join(' ', g.Select(x => x.Words)), StringComparer.OrdinalIgnoreCase);

        var hosts = new List<SliceHost>();
        foreach (var host in terrain.Hosts ?? [])
        {
            if (string.IsNullOrWhiteSpace(host.Name)) continue;
            var image = ImageFor(host.Os, caps, notes, host.Name);
            var console = IsConsole(host.Role);
            var labels = new Dictionary<string, string>();
            var roles = new List<string>();
            if (console) labels[ConsoleLabel] = "true";
            else
            {
                var words = $"{host.Role} {host.Description} {string.Join(' ', host.Services ?? [])} {serviceWords.GetValueOrDefault(host.Name)}";
                roles.AddRange(RolesFor(words, image, caps));
                foreach (var (role, _) in UnconfigurableRoleWords.Where(r => Says(words, r.Words)))
                    notes.Add($"Host {host.Name}: GraySpace's {role} role needs variables the scenario can't give (such as a realm and an admin password), so it is built without it");
                if (placedPool.TryGetValue(host.Name, out var pool))
                {
                    if (Supports(ClientRole, image, caps)) roles.Add(ClientRole);
                    else notes.Add($"Host {host.Name}: image {image.Name} can't run the GHOSTS client, so pool {pool} has no NPC there");
                    labels[PoolLabel] = pool;
                }
            }
            hosts.Add(new SliceHost(Name(host.Name, 40), image.Name, Segment: SegmentOf(host.Segment, $"Host {host.Name}"), Roles: roles,
                Labels: labels.Count > 0 ? labels : null));
        }

        // Pools placed on no host: counted GHOSTS-client hosts, split across the pool's segments.
        var clientImage = ImageFor(null, caps, notes, null);
        var taken = hosts.Select(h => h.Name).ToHashSet();
        foreach (var pool in (input.Pools ?? []).Where(p => p.Count > 0 && p.Hosts is not { Count: > 0 }))
        {
            var poolSegments = (pool.Segments is { Count: > 0 } s ? s : [null]).Select(x => SegmentOf(x, $"Pool {pool.Role}")).Distinct().ToList();
            for (var i = 0; i < poolSegments.Count; i++)
            {
                var count = pool.Count / poolSegments.Count + (i < pool.Count % poolSegments.Count ? 1 : 0);
                if (count == 0) continue;
                var name = Unique(Name(poolSegments.Count == 1 ? pool.Role : $"{pool.Role}-{poolSegments[i]}", 36), taken);
                hosts.Add(new SliceHost(name, clientImage.Name, Count: count > 1 ? count : null, Segment: poolSegments[i],
                    Roles: [ClientRole], Labels: new Dictionary<string, string> { [PoolLabel] = pool.Role }));
            }
        }
        foreach (var pool in (input.Pools ?? []).Where(p => p.Hosts is { Count: > 0 } && p.Count > p.Hosts.Count))
            notes.Add($"Pool {pool.Role} has {pool.Count} NPCs but is placed on {pool.Hosts.Count} hosts; the rest stay unbound");

        // The GHOSTS client role's variables.
        Dictionary<string, Dictionary<string, string>> roleVars = null;
        if (hosts.Any(h => h.Roles?.Contains(ClientRole) == true))
        {
            var vars = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(settings.ClientUrl)) vars["client_url"] = settings.ClientUrl;
            else notes.Add("TerrainProvider:ClientUrl is not set: GraySpace needs the GHOSTS client archive's URL for ghosts-client hosts");
            if (!string.IsNullOrWhiteSpace(settings.GhostsApiUrl))
            {
                vars["api_url"] = settings.GhostsApiUrl;
                if (Uri.TryCreate(settings.GhostsApiUrl, UriKind.Absolute, out var api)) vars["api_host"] = api.Host;
            }
            else notes.Add("TerrainProvider:GhostsApiUrl is not set: GraySpace needs the GHOSTS API URL its clients report to");
            roleVars = new Dictionary<string, Dictionary<string, string>> { [ClientRole] = vars };
        }

        // GraySpace needs an attach to name something; an ISP lets it pick the router with the most free addresses.
        var isp = (caps.Isps ?? []).FirstOrDefault()?.Name;
        if (isp == null) notes.Add("GraySpace lists no ISPs, so the site can't be attached anywhere");
        var spec = new SliceSpec(slug, Prefix(slug), input.Description ?? input.ScenarioName ?? string.Empty, "ghosts",
            [new SliceSite(SiteName, new SliceAttach(Isp: isp), segments)], hosts, roleVars);
        return new EnclaveMapping(spec, notes);
    }

    /// <summary>The same input from a scenario document: its terrain, its population's pools, and its enclave or slug.</summary>
    public static EnclaveInput FromDocument(JsonObject document)
    {
        var terrain = document["terrain"]?.Deserialize<TerrainExtrasDto>(ScenarioExtras.Options) ?? new TerrainExtrasDto();
        var pools = (document["population"]?["pools"] as JsonArray ?? []).OfType<JsonObject>().Select(p => new PoolPlacement(
            p["role"]?.GetValue<string>() ?? string.Empty,
            p["count"] is JsonValue c && c.TryGetValue<int>(out var n) ? n : 0,
            p["segments"]?.Deserialize<List<string>>() ?? [],
            p["hosts"]?.Deserialize<List<string>>() ?? [])).ToList();
        var slug = terrain.Reference?.Enclave ?? terrain.Reference?.Slice ?? document["slug"]?.GetValue<string>();
        return new EnclaveInput(slug, document["name"]?.GetValue<string>(), document["description"]?.GetValue<string>(), terrain, pools);
    }

    private static GraySpaceImage ImageFor(string os, GraySpaceCapabilities caps, List<string> notes, string host)
    {
        var images = (caps.Images ?? []).Where(i => i.Type != "oci").ToList();
        GraySpaceImage Family(string family) =>
            images.Where(i => i.OsFamily == family).OrderBy(i => i.Type == "lxc" ? 0 : 1).FirstOrDefault();
        var fallback = Family("ubuntu") ?? images.FirstOrDefault() ?? new GraySpaceImage("ubuntu-22.04-lxc", "lxc", string.Empty, "ubuntu");
        var text = (os ?? string.Empty).ToLowerInvariant();
        if (text.Length == 0) return fallback;
        foreach (var family in new[] { "windows", "debian", "ubuntu", "alpine" })
        {
            if (!text.Contains(family)) continue;
            if (Family(family) is { } found) return found;
            notes.Add($"Host {host}: GraySpace has no {family} image, so it is built as {fallback.Name}");
            return fallback;
        }
        return fallback;
    }

    private static IEnumerable<string> RolesFor(string words, GraySpaceImage image, GraySpaceCapabilities caps) =>
        RoleWords.Where(r => Says(words, r.Words) && Supports(r.Role, image, caps)).Select(r => r.Role).Distinct();

    private static bool Says(string words, string[] any)
    {
        var text = $" {words.ToLowerInvariant()} ";
        return any.Any(w => Regex.IsMatch(text, $@"\b{Regex.Escape(w)}\b"));
    }

    private static bool Supports(string role, GraySpaceImage image, GraySpaceCapabilities caps) =>
        image.Type != "oci" && (caps.Roles ?? []).Any(r => r.Name == role && (r.OsFamilies ?? []).Contains(image.OsFamily));

    private static bool IsConsole(string role) =>
        (role ?? string.Empty).Contains("participant", StringComparison.OrdinalIgnoreCase)
        || (role ?? string.Empty).Contains("console", StringComparison.OrdinalIgnoreCase);

    private static bool IsPrivate(IPNetwork net) =>
        new[] { "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" }.Select(IPNetwork.Parse)
            .Any(block => block.Contains(net.BaseAddress) && net.PrefixLength >= block.PrefixLength);

    /// <summary>A GraySpace name: lowercase letters, digits and inner hyphens, at most max characters.</summary>
    public static string Name(string text, int max)
    {
        var name = NonName().Replace((text ?? string.Empty).ToLowerInvariant(), "-").Trim('-');
        if (name.Length > max) name = name[..max].TrimEnd('-');
        return name.Length == 0 ? "x" : name;
    }

    /// <summary>The spec's own prefix: the slug's first word, at most eight letters and digits, starting with a letter.</summary>
    private static string Prefix(string slug)
    {
        var word = new string(slug.Split('-')[0].Where(char.IsAsciiLetterOrDigit).ToArray());
        if (word.Length == 0 || !char.IsAsciiLetter(word[0])) word = "e" + word;
        return word.Length > 8 ? word[..8] : word;
    }

    private static string Unique(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var i = 2; !taken.Add(candidate); i++) candidate = $"{name}-{i}";
        return candidate;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonName();
}
