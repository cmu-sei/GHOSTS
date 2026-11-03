// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>
/// GET /scenarios/{id}/kind-update — what updating to the latest published version of its kind would do. When the
/// scenario is already on it, <c>from</c> and <c>to</c> are the same and nothing would change.
/// </summary>
public class KindUpdatePreviewEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/kind-update", async (int id, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            await ScenarioState.Load(db, kinds, id, ct) is { } state
                ? Results.Ok(KindUpdate.Plan(state.Kind, kinds.Find(state.Kind.Id)!, state.Scenario, state.Decisions))
                : ScenarioState.Missing(id))
            .WithTags(tag);
}
