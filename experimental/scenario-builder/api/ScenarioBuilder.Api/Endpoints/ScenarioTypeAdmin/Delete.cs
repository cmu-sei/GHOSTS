// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>DELETE /admin/scenario-types/{id} — only a scenario type never published, which no scenario can be on. Others are retired instead.</summary>
public class DeleteEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/admin/scenario-types/{id}", async (string id, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is not { } row)
                    return ScenarioTypeViews.Missing(id);
                if (await db.PublishedScenarioTypes.AnyAsync(p => p.ScenarioTypeId == id, ct))
                    return ErrorResults.Conflict("SCENARIO_TYPE_PUBLISHED", $"'{id}' has been published, so scenarios may be on it. Retire it instead.");
                db.ScenarioTypes.Remove(row);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
