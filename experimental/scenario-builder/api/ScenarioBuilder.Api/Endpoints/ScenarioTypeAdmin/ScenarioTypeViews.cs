// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

public record ScenarioTypeAdminSummary(string Id, string Name, string? LatestVersion, bool DraftChanged, int Scenarios, bool Retired);

public record ScenarioTypeVersionView(string Version, string PublishedBy, DateTime Published, int Scenarios);

/// <summary>A scenario type as admins see it: its draft, and what's published of it.</summary>
public record ScenarioTypeDetail(
    string Id, ScenarioType ScenarioType, string? BasedOn, bool Retired, string? LatestVersion, bool DraftChanged, int Scenarios, List<ScenarioTypeVersionView> Versions);

public static class ScenarioTypeViews
{
    public static IResult Missing(string id) => ErrorResults.NotFound("SCENARIO_TYPE_UNKNOWN", $"There is no scenario type '{id}'.");

    public static IResult Invalid(string problem) => ErrorResults.BadRequest("SCENARIO_TYPE_INVALID", problem);

    public static ScenarioType Draft(ScenarioTypeRecord row) => ScenarioTypeStore.Read(row.Draft);

    /// <summary>A scenario type's JSON from a request body, or what's wrong with it.</summary>
    public static async Task<(ScenarioType? ScenarioType, IResult? Error)> ReadBody(HttpContext http, CancellationToken ct)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<ScenarioType>(http.Request.Body, ScenarioTypeCatalog.Json, ct) is { } scenarioType
                ? (scenarioType, null)
                : (null, Invalid("That isn't a scenario type: it's empty."));
        }
        catch (JsonException e)
        {
            return (null, Invalid($"That isn't a scenario type: {e.Message}"));
        }
    }

    public static async Task<List<ScenarioTypeAdminSummary>> List(BuilderDb db, CancellationToken ct)
    {
        var versions = (await db.PublishedScenarioTypes.OrderBy(p => p.Id).ToListAsync(ct)).ToLookup(p => p.ScenarioTypeId);
        var scenarios = (await db.Scenarios.GroupBy(s => s.ScenarioTypeId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(g => g.Key, g => g.Count);
        return (await db.ScenarioTypes.OrderBy(k => k.Id).ToListAsync(ct)).Select(row =>
        {
            var draft = Draft(row);
            var latest = versions[row.ScenarioTypeId].LastOrDefault();
            return new ScenarioTypeAdminSummary(row.ScenarioTypeId, draft.Name, latest?.Version, DraftChanged(draft, latest), scenarios.GetValueOrDefault(row.ScenarioTypeId), row.Retired);
        }).ToList();
    }

    public static async Task<ScenarioTypeDetail> Detail(BuilderDb db, ScenarioTypeRecord row, CancellationToken ct)
    {
        var versions = await db.PublishedScenarioTypes.Where(p => p.ScenarioTypeId == row.ScenarioTypeId).OrderBy(p => p.Id).ToListAsync(ct);
        var scenarios = (await db.Scenarios.Where(s => s.ScenarioTypeId == row.ScenarioTypeId).Select(s => s.ScenarioTypeVersion).ToListAsync(ct)).CountBy(v => v)
            .ToDictionary(c => c.Key, c => c.Value);
        var draft = Draft(row);
        var latest = versions.LastOrDefault();
        return new ScenarioTypeDetail(row.ScenarioTypeId, draft, row.BasedOn, row.Retired, latest?.Version, DraftChanged(draft, latest), scenarios.Values.Sum(),
            versions.Select(v => new ScenarioTypeVersionView(v.Version, v.PublishedBy, v.Published, scenarios.GetValueOrDefault(v.Version))).ToList());
    }

    /// <summary>Before the first publish, any stage is a change.</summary>
    private static bool DraftChanged(ScenarioType draft, PublishedScenarioType? latest) =>
        latest is null ? draft.Stages.Count > 0 : !ScenarioTypeStore.SameAs(draft, ScenarioTypeStore.Read(latest.Content));
}
