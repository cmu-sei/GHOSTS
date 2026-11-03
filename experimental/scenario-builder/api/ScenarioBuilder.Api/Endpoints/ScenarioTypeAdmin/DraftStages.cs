// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

public record DraftWith(string? Model);

/// <summary>
/// POST /admin/scenario-types/{id}/draft/stages — the model plans the draft's stages from its name and description, replacing
/// every stage it had. Each stage's questions are written next, one stage at a time.
/// </summary>
public class DraftStagesEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/scenario-types/{id}/draft/stages", async (string id, DraftWith body, ScenarioTypeDrafter drafter, ScenarioTypeCatalog scenarioTypes, IOptions<ModelOptions> models,
                BuilderDb db, CancellationToken ct) =>
            {
                if (await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is not { } row)
                    return ScenarioTypeViews.Missing(id);
                if (!drafter.Knows(body.Model))
                    return UnknownModel(body.Model, models.Value);
                try
                {
                    var planned = await drafter.PlanStages(body.Model!, ScenarioTypeViews.Draft(row), Example(row, scenarioTypes), ct);
                    row.Draft = ScenarioTypeStore.Write(planned);
                    row.Updated = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(await ScenarioTypeViews.Detail(db, row, ct));
                }
                catch (DraftFailed e)
                {
                    return ErrorResults.BadGateway("MODEL_FAILED", e.Message);
                }
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);

    public static IResult UnknownModel(string? model, ModelOptions options) =>
        ErrorResults.BadRequest("MODEL_UNKNOWN", $"No model named '{model}'. Configured: {string.Join(", ", options.Models.Select(m => m.Name))}.");

    /// <summary>What the model is shown for format and depth: the scenario type this one started from, or else the first kind offered.</summary>
    public static ScenarioType Example(ScenarioTypeRecord row, ScenarioTypeCatalog scenarioTypes) =>
        (row.BasedOn is not null ? scenarioTypes.Find(row.BasedOn) : null) ?? scenarioTypes.Offered.FirstOrDefault() ?? scenarioTypes.All[0];
}
