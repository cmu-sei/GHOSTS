// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>
/// Where scenario types are kept. A fresh database starts with every <c>.scenario-type.json</c> file, each published at its own version
/// by <c>system</c>; once any scenario type exists the files are ignored, so an admin's changes are never overwritten.
/// </summary>
public static class ScenarioTypeStore
{
    public const string By = "system";

    /// <summary>What a scenario type is before its first publish.</summary>
    public const string Unpublished = "0.0.0";

    private static readonly JsonSerializerOptions File = new(ScenarioTypeCatalog.Json) { WriteIndented = true };

    public static ScenarioType Read(string json) => JsonSerializer.Deserialize<ScenarioType>(json, ScenarioTypeCatalog.Json)!;

    public static string Write(ScenarioType scenarioType) => JsonSerializer.Serialize(scenarioType, File);

    /// <summary>Same content, whatever the version.</summary>
    public static bool SameAs(ScenarioType a, ScenarioType b) =>
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a with { Version = "" }, ScenarioTypeCatalog.Json), JsonSerializer.SerializeToNode(b with { Version = "" }, ScenarioTypeCatalog.Json));

    /// <summary>The version after <paramref name="latest"/>: its minor number up one, so 0.3.0 is followed by 0.4.0.</summary>
    public static string Next(string? latest)
    {
        if (latest is null)
            return "0.1.0";
        var parts = latest.Split('.');
        var major = int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 0;
        return $"{major}.{minor + 1}.0";
    }

    public static async Task Seed(BuilderDb db, string directory, CancellationToken ct)
    {
        if (await db.ScenarioTypes.AnyAsync(ct))
            return;
        foreach (var scenarioType in ScenarioTypeCatalog.ReadDirectory(directory))
            await Publish(db, null, scenarioType, scenarioType.Version, By, ct);
    }

    /// <summary>Fills <paramref name="catalog"/> with every published version, oldest first, and marks the retired scenario types.</summary>
    public static async Task Load(BuilderDb db, ScenarioTypeCatalog catalog, CancellationToken ct)
    {
        var published = (await db.PublishedScenarioTypes.OrderBy(p => p.Id).ToListAsync(ct)).ToLookup(p => p.ScenarioTypeId);
        foreach (var scenarioType in await db.ScenarioTypes.OrderBy(k => k.Id).ToListAsync(ct))
        {
            foreach (var version in published[scenarioType.ScenarioTypeId])
                catalog.Add(Read(version.Content));
            catalog.SetRetired(scenarioType.ScenarioTypeId, scenarioType.Retired);
        }
    }

    /// <summary>
    /// The one way a version is made: <paramref name="scenarioType"/> is stored as <paramref name="version"/>, its draft is reset to
    /// it (a new scenario type is made for an id not seen before), and the catalog gets it. Seeding, publish and import all use it.
    /// </summary>
    public static async Task<PublishedScenarioType> Publish(BuilderDb db, ScenarioTypeCatalog? catalog, ScenarioType scenarioType, string version, string by, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var content = Write(scenarioType with { Version = version });
        var row = await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == scenarioType.Id, ct);
        if (row is null)
        {
            row = new ScenarioTypeRecord { ScenarioTypeId = scenarioType.Id, Created = now };
            db.ScenarioTypes.Add(row);
        }
        row.Draft = content;
        row.Updated = now;
        var published = new PublishedScenarioType { ScenarioTypeId = scenarioType.Id, Version = version, Content = content, PublishedBy = by, Published = now };
        db.PublishedScenarioTypes.Add(published);
        await db.SaveChangesAsync(ct);
        catalog?.Add(Read(content));
        return published;
    }
}
