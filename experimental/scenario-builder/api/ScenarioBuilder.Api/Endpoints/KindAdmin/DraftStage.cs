// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>
/// POST /admin/kinds/{id}/draft/stages/{n} — the model writes stage n's questions, replacing the ones it had. When it
/// can't, the draft is left as it was.
/// </summary>
public class DraftStageEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/kinds/{id}/draft/stages/{n:int}", async (string id, int n, DraftWith body, KindDrafter drafter, KindCatalog kinds,
                IOptions<ModelOptions> models, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is not { } row)
                    return KindViews.Missing(id);
                if (!drafter.Knows(body.Model))
                    return DraftStagesEndpoint.UnknownModel(body.Model, models.Value);
                var draft = KindViews.Draft(row);
                if (n < 0 || n >= draft.Stages.Count)
                    return ErrorResults.NotFound("STAGE_UNKNOWN", $"The draft has no stage {n}. It has {draft.Stages.Count}; plan the stages first.");
                try
                {
                    var written = await drafter.WriteStage(body.Model!, draft, n, DraftStagesEndpoint.Example(row, kinds), ct);
                    row.Draft = KindStore.Write(written);
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
}
