// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>
/// GET /scenarios/{id}/scenario-type-update — what updating to the latest published version of its scenario type would do. When the
/// scenario is already on it, <c>from</c> and <c>to</c> are the same and nothing would change.
/// </summary>
public class ScenarioTypeUpdatePreviewEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/scenario-type-update", async (int id, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
            await ScenarioState.Load(db, scenarioTypes, id, ct) is { } state
                ? Results.Ok(ScenarioTypeUpdate.Plan(state.ScenarioType, scenarioTypes.Find(state.ScenarioType.Id)!, state.Scenario, state.Decisions))
                : ScenarioState.Missing(id))
            .WithTags(tag);
}
