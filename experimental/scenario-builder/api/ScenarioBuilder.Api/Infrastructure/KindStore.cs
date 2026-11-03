// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>
/// Where kinds are kept. A fresh database starts with every <c>.kind.json</c> file, each published at its own version
/// by <c>system</c>; once any kind exists the files are ignored, so an admin's changes are never overwritten.
/// </summary>
public static class KindStore
{
    public const string By = "system";

    /// <summary>What a kind is before its first publish.</summary>
    public const string Unpublished = "0.0.0";

    private static readonly JsonSerializerOptions File = new(KindCatalog.Json) { WriteIndented = true };

    public static Kind Read(string json) => JsonSerializer.Deserialize<Kind>(json, KindCatalog.Json)!;

    public static string Write(Kind kind) => JsonSerializer.Serialize(kind, File);

    /// <summary>Same content, whatever the version.</summary>
    public static bool SameAs(Kind a, Kind b) =>
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(a with { Version = "" }, KindCatalog.Json), JsonSerializer.SerializeToNode(b with { Version = "" }, KindCatalog.Json));

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
        if (await db.Kinds.AnyAsync(ct))
            return;
        foreach (var kind in KindCatalog.ReadDirectory(directory))
            await Publish(db, null, kind, kind.Version, By, ct);
    }

    /// <summary>Fills <paramref name="catalog"/> with every published version, oldest first, and marks the retired kinds.</summary>
    public static async Task Load(BuilderDb db, KindCatalog catalog, CancellationToken ct)
    {
        var published = (await db.PublishedKinds.OrderBy(p => p.Id).ToListAsync(ct)).ToLookup(p => p.KindId);
        foreach (var kind in await db.Kinds.OrderBy(k => k.Id).ToListAsync(ct))
        {
            foreach (var version in published[kind.KindId])
                catalog.Add(Read(version.Content));
            catalog.SetRetired(kind.KindId, kind.Retired);
        }
    }

    /// <summary>
    /// The one way a version is made: <paramref name="kind"/> is stored as <paramref name="version"/>, its draft is reset to
    /// it (a new kind is made for an id not seen before), and the catalog gets it. Seeding, publish and import all use it.
    /// </summary>
    public static async Task<PublishedKind> Publish(BuilderDb db, KindCatalog? catalog, Kind kind, string version, string by, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var content = Write(kind with { Version = version });
        var row = await db.Kinds.FirstOrDefaultAsync(k => k.KindId == kind.Id, ct);
        if (row is null)
        {
            row = new ScenarioKind { KindId = kind.Id, Created = now };
            db.Kinds.Add(row);
        }
        row.Draft = content;
        row.Updated = now;
        var published = new PublishedKind { KindId = kind.Id, Version = version, Content = content, PublishedBy = by, Published = now };
        db.PublishedKinds.Add(published);
        await db.SaveChangesAsync(ct);
        catalog?.Add(Read(content));
        return published;
    }
}
