// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

public record AdvanceView(int Stage);

/// <summary>POST /scenarios/{id}/advance — open the next stage, when the open stage's gate passes. The agent or the person may.</summary>
public class AdvanceEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/advance", async (int id, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
        {
            var state = await ScenarioState.Load(db, kinds, id, ct);
            if (state is null)
                return ScenarioState.Missing(id);
            var progress = state.Progress();
            if (!progress.Gate.Passes)
                return ErrorResults.Conflict("GATE_CLOSED",
                    $"Stage {progress.Stage} can't close yet: {string.Join(" ", progress.Gate.Findings.Select(f => f.Message))}");
            if (progress.LastStage)
                return ErrorResults.Conflict("LAST_STAGE",
                    $"Stage {progress.Stage} is the last stage in this version of the kind. Its gate has passed; the work is done.");

            state.Scenario.Stage++;
            state.Scenario.Updated = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new AdvanceView(state.Scenario.Stage));
        }).WithTags(tag);
}
