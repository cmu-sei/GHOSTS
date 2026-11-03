// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>
/// POST /admin/scenario-types/import — a <c>.scenario-type.json</c> file as the body, published as it comes. A new id becomes a new scenario type
/// at the file's version; a known id gets its next version. Either way the draft is reset to the file.
/// </summary>
public class ImportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/scenario-types/import", async (HttpContext http, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
            {
                var (scenarioType, error) = await ScenarioTypeViews.ReadBody(http, ct);
                if (error is not null)
                    return error;
                if (ScenarioTypeCatalog.Problem(scenarioType!) is { } problem)
                    return ScenarioTypeViews.Invalid(problem);
                var latest = await db.PublishedScenarioTypes.Where(p => p.ScenarioTypeId == scenarioType!.Id).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
                var version = latest is not null ? ScenarioTypeStore.Next(latest.Version) : string.IsNullOrWhiteSpace(scenarioType!.Version) ? ScenarioTypeStore.Next(null) : scenarioType.Version;
                await ScenarioTypeStore.Publish(db, scenarioTypes, scenarioType!, version, Caller.From(http).User, ct);
                var row = await db.ScenarioTypes.FirstAsync(k => k.ScenarioTypeId == scenarioType!.Id, ct);
                return Results.Ok(await ScenarioTypeViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
