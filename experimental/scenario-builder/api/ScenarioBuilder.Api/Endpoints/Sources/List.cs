// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

/// <summary>GET /scenarios/{id}/sources — the context given for a scenario, in the order it was added, without its text.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/sources", async (int id, BuilderDb db, CancellationToken ct) =>
                await db.Scenarios.FindAsync([id], ct) is null
                    ? ScenarioState.Missing(id)
                    : Results.Ok((await db.Sources.Where(s => s.ScenarioId == id).OrderBy(s => s.Id).ToListAsync(ct)).Select(SourceView.From)))
            .WithTags(tag);
}
