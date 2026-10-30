// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Outputs;

public record DocumentView(string Library, string LibraryName, int Version, string Template, string Name, Audience Audience, TemplateFormat Format);

/// <summary>GET /scenarios/{id}/documents — every template in the latest published version of each library for the scenario's kind.</summary>
public class DocumentsEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/documents", async (int id, BuilderDb db, CancellationToken ct) =>
                await db.Scenarios.FindAsync([id], ct) is { } scenario
                    ? Results.Ok((await Published(db, scenario.KindId, ct))
                        .SelectMany(f => f.Templates.Select(t => new DocumentView(f.Slug, f.Name, f.Version, t.Id, t.Name, t.Audience, t.Format))))
                    : ScenarioState.Missing(id))
            .WithTags(tag);

    /// <summary>The latest version of every library serving <paramref name="kindId"/>, by library name.</summary>
    public static async Task<List<LibraryFile>> Published(BuilderDb db, string kindId, CancellationToken ct) =>
        (await db.LibraryVersions.ToListAsync(ct))
        .GroupBy(v => v.LibraryId)
        .Select(g => LibraryFile.Read(g.MaxBy(v => v.Number)!.Content))
        .Where(f => f.Kinds.Contains(kindId))
        .OrderBy(f => f.Name)
        .ToList();
}
