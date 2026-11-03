// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

public record KindAdminSummary(string Id, string Name, string? LatestVersion, bool DraftChanged, int Scenarios, bool Retired);

public record KindVersionView(string Version, string PublishedBy, DateTime Published, int Scenarios);

/// <summary>A kind as admins see it: its draft, and what's published of it.</summary>
public record KindDetail(
    string Id, Kind Kind, string? BasedOn, bool Retired, string? LatestVersion, bool DraftChanged, int Scenarios, List<KindVersionView> Versions);

public static class KindViews
{
    public static IResult Missing(string id) => ErrorResults.NotFound("KIND_UNKNOWN", $"There is no scenario type '{id}'.");

    public static IResult Invalid(string problem) => ErrorResults.BadRequest("KIND_INVALID", problem);

    public static Kind Draft(ScenarioKind row) => KindStore.Read(row.Draft);

    /// <summary>A kind's JSON from a request body, or what's wrong with it.</summary>
    public static async Task<(Kind? Kind, IResult? Error)> ReadBody(HttpContext http, CancellationToken ct)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<Kind>(http.Request.Body, KindCatalog.Json, ct) is { } kind
                ? (kind, null)
                : (null, Invalid("That isn't a scenario type: it's empty."));
        }
        catch (JsonException e)
        {
            return (null, Invalid($"That isn't a scenario type: {e.Message}"));
        }
    }

    public static async Task<List<KindAdminSummary>> List(BuilderDb db, CancellationToken ct)
    {
        var versions = (await db.PublishedKinds.OrderBy(p => p.Id).ToListAsync(ct)).ToLookup(p => p.KindId);
        var scenarios = (await db.Scenarios.GroupBy(s => s.KindId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(g => g.Key, g => g.Count);
        return (await db.Kinds.OrderBy(k => k.Id).ToListAsync(ct)).Select(row =>
        {
            var draft = Draft(row);
            var latest = versions[row.KindId].LastOrDefault();
            return new KindAdminSummary(row.KindId, draft.Name, latest?.Version, DraftChanged(draft, latest), scenarios.GetValueOrDefault(row.KindId), row.Retired);
        }).ToList();
    }

    public static async Task<KindDetail> Detail(BuilderDb db, ScenarioKind row, CancellationToken ct)
    {
        var versions = await db.PublishedKinds.Where(p => p.KindId == row.KindId).OrderBy(p => p.Id).ToListAsync(ct);
        var scenarios = (await db.Scenarios.Where(s => s.KindId == row.KindId).Select(s => s.KindVersion).ToListAsync(ct)).CountBy(v => v)
            .ToDictionary(c => c.Key, c => c.Value);
        var draft = Draft(row);
        var latest = versions.LastOrDefault();
        return new KindDetail(row.KindId, draft, row.BasedOn, row.Retired, latest?.Version, DraftChanged(draft, latest), scenarios.Values.Sum(),
            versions.Select(v => new KindVersionView(v.Version, v.PublishedBy, v.Published, scenarios.GetValueOrDefault(v.Version))).ToList());
    }

    /// <summary>Before the first publish, any stage is a change.</summary>
    private static bool DraftChanged(Kind draft, PublishedKind? latest) =>
        latest is null ? draft.Stages.Count > 0 : !KindStore.SameAs(draft, KindStore.Read(latest.Content));
}
