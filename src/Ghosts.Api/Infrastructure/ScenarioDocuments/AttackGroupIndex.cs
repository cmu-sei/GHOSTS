// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// Every MITRE ATT&amp;CK intrusion-set (group) id an authoring agent can name an adversary from, with
/// its name, its known aliases, its domains and whether MITRE has revoked or deprecated it. Embedded
/// in the assembly from schemas/scenario-document/corpus/attack-groups.json, which
/// corpus/build-attack-index.mjs builds from the same MITRE CTI STIX bundles, at the same commit, as
/// <see cref="AttackIndex"/>. Naming a real adversary is a mission judgment (ELICITATION.md E3); this
/// index exists so the id attached to that name is a lookup, not a guess — the same failure mode
/// AttackIndex exists to prevent for technique ids.
/// </summary>
public static class AttackGroupIndex
{
    public record Group(string Id, string Name, IReadOnlyList<string> Aliases, IReadOnlyList<string> Domains, bool Revoked, bool Deprecated);

    private const string Resource = "Ghosts.Api.ScenarioDocuments.attack-groups.json";

    private static readonly Lazy<Loaded> Index = new(Load);

    private record Loaded(IReadOnlyDictionary<string, Group> Groups, string Commit, string BuiltAt);

    /// <summary>The group, or null when no such id exists.</summary>
    public static Group Find(string id) =>
        id != null && Index.Value.Groups.TryGetValue(id, out var g) ? g : null;

    /// <summary>
    /// Groups matching an id, a fragment of the primary name, or a fragment of any alias — "Sandworm"
    /// and "Voodoo Bear" both find G0034. An exact id first, then an id prefix, then the name, then an
    /// alias. Revoked and deprecated groups are returned rather than hidden, for the same reason
    /// AttackIndex.Search returns dead techniques: the caller needs to be told before it authors
    /// against a retired id.
    /// </summary>
    public static IReadOnlyList<Group> Search(string query, int take)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var q = query.Trim();

        return Index.Value.Groups.Values
            .Select(g => (g, rank: Rank(g, q)))
            .Where(x => x.rank > 0)
            .OrderBy(x => x.rank)
            .ThenBy(x => x.g.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => x.g)
            .ToList();
    }

    private static int Rank(Group g, string q) =>
        string.Equals(g.Id, q, StringComparison.OrdinalIgnoreCase) ? 1
        : g.Id.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 2
        : g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ? 3
        : g.Aliases.Any(a => a.Contains(q, StringComparison.OrdinalIgnoreCase)) ? 4
        : 0;

    /// <summary>The bundle commit the index was built from, for the log and the info finding.</summary>
    public static string Provenance =>
        $"attack-stix-data {(Index.Value.Commit is { Length: >= 7 } sha ? sha[..7] : "unknown")} built {Index.Value.BuiltAt}";

    public static int Count => Index.Value.Groups.Count;

    private static Loaded Load()
    {
        using var doc = JsonDocument.Parse(ScenarioDocumentSchema.Read(Resource));
        var root = doc.RootElement;
        var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var g in root.GetProperty("groups").EnumerateArray())
        {
            var domains = new List<string>();
            foreach (var d in g.GetProperty("domains").EnumerateArray()) domains.Add(d.GetString());
            var aliases = new List<string>();
            if (g.TryGetProperty("aliases", out var al))
                foreach (var a in al.EnumerateArray()) aliases.Add(a.GetString());
            var id = g.GetProperty("id").GetString();
            groups[id] = new Group(
                id,
                g.GetProperty("name").GetString(),
                aliases,
                domains,
                g.TryGetProperty("revoked", out var r) && r.GetBoolean(),
                g.TryGetProperty("deprecated", out var p) && p.GetBoolean());
        }
        var builtFrom = root.GetProperty("builtFrom");
        return new Loaded(
            groups,
            builtFrom.TryGetProperty("commit", out var c) && c.ValueKind == JsonValueKind.Object
                ? c.GetProperty("sha").GetString()
                : null,
            builtFrom.TryGetProperty("builtAt", out var b) ? b.GetString() : null);
    }
}
