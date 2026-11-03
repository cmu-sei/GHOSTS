// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

public record DraftWith(string? Model);

/// <summary>
/// POST /admin/kinds/{id}/draft/stages — the model plans the draft's stages from its name and description, replacing
/// every stage it had. Each stage's questions are written next, one stage at a time.
/// </summary>
public class DraftStagesEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/kinds/{id}/draft/stages", async (string id, DraftWith body, KindDrafter drafter, KindCatalog kinds, IOptions<ModelOptions> models,
                BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is not { } row)
                    return KindViews.Missing(id);
                if (!drafter.Knows(body.Model))
                    return UnknownModel(body.Model, models.Value);
                try
                {
                    var planned = await drafter.PlanStages(body.Model!, KindViews.Draft(row), Example(row, kinds), ct);
                    row.Draft = KindStore.Write(planned);
                    row.Updated = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return Results.Ok(await KindViews.Detail(db, row, ct));
                }
                catch (DraftFailed e)
                {
                    return ErrorResults.BadGateway("MODEL_FAILED", e.Message);
                }
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);

    public static IResult UnknownModel(string? model, ModelOptions options) =>
        ErrorResults.BadRequest("MODEL_UNKNOWN", $"No model named '{model}'. Configured: {string.Join(", ", options.Models.Select(m => m.Name))}.");

    /// <summary>What the model is shown for format and depth: the kind this one started from, or else the first kind offered.</summary>
    public static Kind Example(ScenarioKind row, KindCatalog kinds) =>
        (row.BasedOn is not null ? kinds.Find(row.BasedOn) : null) ?? kinds.Offered.FirstOrDefault() ?? kinds.All[0];
}
