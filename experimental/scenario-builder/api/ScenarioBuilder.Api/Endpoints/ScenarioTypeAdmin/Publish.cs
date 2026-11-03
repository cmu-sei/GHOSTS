// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>
/// POST /admin/scenario-types/{id}/publish — the draft, finished, becomes the next version, which new scenarios get.
/// Scenarios already made stay on the version they were made on.
/// </summary>
public class PublishEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/scenario-types/{id}/publish", async (string id, HttpContext http, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is not { } row)
                    return ScenarioTypeViews.Missing(id);
                var draft = ScenarioTypeViews.Draft(row);
                if (ScenarioTypeCatalog.Problem(draft) is { } problem)
                    return ScenarioTypeViews.Invalid($"It can't be published yet: {problem}");
                var latest = await db.PublishedScenarioTypes.Where(p => p.ScenarioTypeId == id).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
                if (latest is not null && ScenarioTypeStore.SameAs(draft, ScenarioTypeStore.Read(latest.Content)))
                    return ErrorResults.Conflict("NOTHING_TO_PUBLISH", $"Nothing has changed since version {latest.Version}.");

                var published = await ScenarioTypeStore.Publish(db, scenarioTypes, draft, ScenarioTypeStore.Next(latest?.Version), Caller.From(http).User, ct);
                return Results.Ok(new ScenarioTypeVersionView(published.Version, published.PublishedBy, published.Published, 0));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
