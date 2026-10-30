// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

public record CreateLibrary(string? Name, string? Description, List<string>? Kinds);

/// <summary>POST /admin/libraries — a new library with an empty draft.</summary>
public class CreateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/libraries", async (CreateLibrary body, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ErrorResults.BadRequest("NAME_REQUIRED", "Give the library a name.");
                var known = kinds.All.Select(k => k.Id).ToList();
                if ((body.Kinds ?? []).FirstOrDefault(k => !known.Contains(k)) is { } unknown)
                    return ErrorResults.BadRequest("KIND_UNKNOWN", $"There is no kind '{unknown}'. Kinds: {string.Join(", ", known)}.");

                var taken = (await db.Libraries.Select(l => l.Slug).ToListAsync(ct)).ToHashSet();
                var now = DateTime.UtcNow;
                var library = new TemplateLibrary
                {
                    Slug = Slug.Unique(name, taken),
                    Name = name,
                    Description = body.Description,
                    Kinds = JsonSerializer.Serialize(body.Kinds ?? [], LibraryFile.Json),
                    Created = now,
                    Updated = now
                };
                db.Libraries.Add(library);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/admin/libraries/{library.Id}", await LibraryViews.Detail(db, library, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
