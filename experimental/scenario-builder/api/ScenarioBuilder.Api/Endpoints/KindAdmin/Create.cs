// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

public record CreateKind(string? Name, string? Description, bool? Topology, string? BasedOn);

/// <summary>
/// POST /admin/kinds — a new kind, unpublished. Based on another kind, its draft starts as a copy of that kind's latest
/// version; otherwise it starts with no stages, for the model to plan.
/// </summary>
public class CreateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/kinds", async (CreateKind body, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ErrorResults.BadRequest("NAME_REQUIRED", "Give the scenario type a name.");
                Kind? basis = null;
                if (body.BasedOn is not null && (basis = kinds.Find(body.BasedOn)) is null)
                    return ErrorResults.BadRequest("KIND_UNKNOWN", $"There is no scenario type '{body.BasedOn}' to start from. Scenario types: {string.Join(", ", kinds.All.Select(k => k.Id))}.");

                var taken = (await db.Kinds.Select(k => k.KindId).ToListAsync(ct)).ToHashSet();
                var id = Slug.Unique(name, taken);
                var description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
                var draft = (basis ?? new Kind(id, KindStore.Unpublished, name, [], []))
                    with { Id = id, Version = KindStore.Unpublished, Name = name, Description = description, Topology = body.Topology ?? false };
                var now = DateTime.UtcNow;
                var row = new ScenarioKind { KindId = id, Draft = KindStore.Write(draft), BasedOn = basis?.Id, Created = now, Updated = now };
                db.Kinds.Add(row);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/admin/kinds/{id}", await KindViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
