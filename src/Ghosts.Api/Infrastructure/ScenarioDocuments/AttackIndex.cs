// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// Every ATT&amp;CK technique id the validator will accept, with its name, its domains and whether MITRE
/// has revoked or deprecated it. Embedded in the assembly from schemas/scenario-document/corpus/
/// attack-index.json, which corpus/build-attack-index.mjs builds from the MITRE CTI STIX bundles. The
/// index is a resolver, not a copy: the document holds ids and the standard holds the content.
/// </summary>
public static class AttackIndex
{
    public record Technique(string Id, string Name, IReadOnlyList<string> Domains, bool Revoked, bool Deprecated);

    private const string Resource = "Ghosts.Api.ScenarioDocuments.attack-index.json";

    private static readonly Lazy<Loaded> Index = new(Load);

    private record Loaded(IReadOnlyDictionary<string, Technique> Techniques, string Commit, string BuiltAt);

    /// <summary>The technique, or null when no such id exists in any domain.</summary>
    public static Technique Find(string id) =>
        id != null && Index.Value.Techniques.TryGetValue(id, out var t) ? t : null;

    /// <summary>
    /// Techniques matching an id or a fragment of a name, so an author can find one without recalling
    /// it. An exact id first, then ids that start with the query, then names that contain it; revoked
    /// and deprecated techniques are returned rather than hidden, because the caller needs to be told
    /// the id it was about to use is dead.
    /// </summary>
    public static IReadOnlyList<Technique> Search(string query, int take)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var q = query.Trim();

        return Index.Value.Techniques.Values
            .Select(t => (t, rank: Rank(t, q)))
            .Where(x => x.rank > 0)
            .OrderBy(x => x.rank)
            .ThenBy(x => x.t.Id, StringComparer.Ordinal)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => x.t)
            .ToList();
    }

    private static int Rank(Technique t, string q) =>
        string.Equals(t.Id, q, StringComparison.OrdinalIgnoreCase) ? 1
        : t.Id.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 2
        : t.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ? 3
        : 0;

    /// <summary>The bundle commit the index was built from, for the log and the info finding.</summary>
    public static string Provenance =>
        $"attack-stix-data {(Index.Value.Commit is { Length: >= 7 } sha ? sha[..7] : "unknown")} built {Index.Value.BuiltAt}";

    public static int Count => Index.Value.Techniques.Count;

    private static Loaded Load()
    {
        using var doc = JsonDocument.Parse(ScenarioDocumentSchema.Read(Resource));
        var root = doc.RootElement;
        var techniques = new Dictionary<string, Technique>(StringComparer.Ordinal);
        foreach (var t in root.GetProperty("techniques").EnumerateArray())
        {
            var domains = new List<string>();
            foreach (var d in t.GetProperty("domains").EnumerateArray()) domains.Add(d.GetString());
            var id = t.GetProperty("id").GetString();
            techniques[id] = new Technique(
                id,
                t.GetProperty("name").GetString(),
                domains,
                t.TryGetProperty("revoked", out var r) && r.GetBoolean(),
                t.TryGetProperty("deprecated", out var p) && p.GetBoolean());
        }
        var builtFrom = root.GetProperty("builtFrom");
        return new Loaded(
            techniques,
            builtFrom.TryGetProperty("commit", out var c) && c.ValueKind == JsonValueKind.Object
                ? c.GetProperty("sha").GetString()
                : null,
            builtFrom.TryGetProperty("builtAt", out var b) ? b.GetString() : null);
    }
}
