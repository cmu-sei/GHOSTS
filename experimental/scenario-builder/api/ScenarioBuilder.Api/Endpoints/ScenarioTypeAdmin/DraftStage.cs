// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>
/// POST /admin/scenario-types/{id}/draft/stages/{n} — the model writes stage n's questions, replacing the ones it had. When it
/// can't, the draft is left as it was.
/// </summary>
public class DraftStageEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/scenario-types/{id}/draft/stages/{n:int}", async (string id, int n, DraftWith body, ScenarioTypeDrafter drafter, ScenarioTypeCatalog scenarioTypes,
                IOptions<ModelOptions> models, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is not { } row)
                    return ScenarioTypeViews.Missing(id);
                if (!drafter.Knows(body.Model))
                    return DraftStagesEndpoint.UnknownModel(body.Model, models.Value);
                var draft = ScenarioTypeViews.Draft(row);
                if (n < 0 || n >= draft.Stages.Count)
                    return ErrorResults.NotFound("STAGE_UNKNOWN", $"The draft has no stage {n}. It has {draft.Stages.Count}; plan the stages first.");
                try
                {
                    var written = await drafter.WriteStage(body.Model!, draft, n, DraftStagesEndpoint.Example(row, scenarioTypes), ct);
                    row.Draft = ScenarioTypeStore.Write(written);
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
}
