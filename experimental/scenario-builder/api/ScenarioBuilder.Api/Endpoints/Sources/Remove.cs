// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

/// <summary>DELETE /scenarios/{id}/sources/{sourceId} — the source is gone; answers already drawn from it stay.</summary>
public class RemoveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/scenarios/{id:int}/sources/{sourceId:int}", async (int id, int sourceId, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (SourceRules.AgentRefused(Caller.From(http)) is { } refused)
                    return refused;
                var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId && s.ScenarioId == id, ct);
                if (source is null)
                    return ErrorResults.NotFound("SOURCE_UNKNOWN", $"Scenario {id} has no source {sourceId}.");
                db.Sources.Remove(source);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .WithTags(tag);
}
