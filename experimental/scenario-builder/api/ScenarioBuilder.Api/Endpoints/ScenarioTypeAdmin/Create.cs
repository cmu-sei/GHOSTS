// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

public record CreateScenarioType(string? Name, string? Description, bool? Topology, string? BasedOn);

/// <summary>
/// POST /admin/scenario-types — a new scenario type, unpublished. Based on another scenario type, its draft starts as a copy of that scenario type's latest
/// version; otherwise it starts with no stages, for the model to plan.
/// </summary>
public class CreateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/scenario-types", async (CreateScenarioType body, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
            {
                var name = body.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                    return ErrorResults.BadRequest("NAME_REQUIRED", "Give the scenario type a name.");
                ScenarioType? basis = null;
                if (body.BasedOn is not null && (basis = scenarioTypes.Find(body.BasedOn)) is null)
                    return ErrorResults.BadRequest("SCENARIO_TYPE_UNKNOWN", $"There is no scenario type '{body.BasedOn}' to start from. Scenario types: {string.Join(", ", scenarioTypes.All.Select(k => k.Id))}.");

                var taken = (await db.ScenarioTypes.Select(k => k.ScenarioTypeId).ToListAsync(ct)).ToHashSet();
                var id = Slug.Unique(name, taken);
                var description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
                var draft = (basis ?? new ScenarioType(id, ScenarioTypeStore.Unpublished, name, [], []))
                    with { Id = id, Version = ScenarioTypeStore.Unpublished, Name = name, Description = description, Topology = body.Topology ?? false };
                var now = DateTime.UtcNow;
                var row = new ScenarioTypeRecord { ScenarioTypeId = id, Draft = ScenarioTypeStore.Write(draft), BasedOn = basis?.Id, Created = now, Updated = now };
                db.ScenarioTypes.Add(row);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/admin/scenario-types/{id}", await ScenarioTypeViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
