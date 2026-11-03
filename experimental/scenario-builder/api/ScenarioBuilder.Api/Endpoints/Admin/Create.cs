// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

public record CreateLibrary(string? Name, string? Description, List<string>? ScenarioTypes);

/// <summary>POST /admin/libraries — a new library with an empty draft.</summary>
public class CreateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/libraries", async (CreateLibrary body, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
            {
                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ErrorResults.BadRequest("NAME_REQUIRED", "Give the library a name.");
                var known = scenarioTypes.All.Select(k => k.Id).ToList();
                if ((body.ScenarioTypes ?? []).FirstOrDefault(k => !known.Contains(k)) is { } unknown)
                    return ErrorResults.BadRequest("SCENARIO_TYPE_UNKNOWN", $"There is no scenario type '{unknown}'. Scenario types: {string.Join(", ", known)}.");

                var taken = (await db.Libraries.Select(l => l.Slug).ToListAsync(ct)).ToHashSet();
                var now = DateTime.UtcNow;
                var library = new TemplateLibrary
                {
                    Slug = Slug.Unique(name, taken),
                    Name = name,
                    Description = body.Description,
                    ScenarioTypes = JsonSerializer.Serialize(body.ScenarioTypes ?? [], LibraryFile.Json),
                    Created = now,
                    Updated = now
                };
                db.Libraries.Add(library);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/admin/libraries/{library.Id}", await LibraryViews.Detail(db, library, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
