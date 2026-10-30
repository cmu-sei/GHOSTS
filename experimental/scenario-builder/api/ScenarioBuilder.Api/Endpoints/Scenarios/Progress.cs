// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>GET /scenarios/{id}/progress — the open stage, its open questions in order, the gate, every answer so far, and the sources.</summary>
public class ProgressEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/progress", async (int id, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                if (await ScenarioState.Load(db, kinds, id, ct) is not { } state)
                    return ScenarioState.Missing(id);
                var sources = await db.Sources.Where(s => s.ScenarioId == id).OrderBy(s => s.Id)
                    .Select(s => new SourceSummary(s.Id, s.Name, s.Kind, s.Text.Length)).ToListAsync(ct);
                return Results.Ok(state.Progress() with { Sources = sources });
            })
            .WithTags(tag);
}
