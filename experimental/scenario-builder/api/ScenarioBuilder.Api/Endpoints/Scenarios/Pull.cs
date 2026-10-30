// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>
/// GET /scenarios/{id} — the pull API: the full scenario JSON (schema/exercise-document.v0.schema.json), with its
/// status and open findings. Every consumer decides for itself whether it accepts a draft.
/// </summary>
public class PullEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}", async (int id, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
                await ScenarioState.Load(db, kinds, id, ct) is { } s
                    ? Results.Ok(ScenarioDocument.Build(s.Kind, s.Scenario, s.Decisions, s.Approvals))
                    : ScenarioState.Missing(id))
            .WithTags(tag);
}
