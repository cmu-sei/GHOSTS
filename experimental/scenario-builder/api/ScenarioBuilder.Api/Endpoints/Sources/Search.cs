// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

public record SourceRef(int Id, string Name);

public record PassageView(SourceRef Source, int Passage, int Of, string Text);

/// <summary>GET /scenarios/{id}/sources/search?q= — the passages of the scenario's sources that share the most words with q.</summary>
public class SearchEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/sources/search", async (int id, string? q, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Scenarios.FindAsync([id], ct) is null)
                    return ScenarioState.Missing(id);
                var sources = await db.Sources.Where(s => s.ScenarioId == id).OrderBy(s => s.Id).ToListAsync(ct);
                return Results.Ok(SourceSearch.Search(sources, q ?? "")
                    .Select(p => new PassageView(new SourceRef(p.Source.Id, p.Source.Name), p.Number, p.Of, p.Text)));
            })
            .WithTags(tag);
}
