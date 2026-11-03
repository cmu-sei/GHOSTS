// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Scenarios;

/// <summary>GET /scenarios — every scenario, most recently changed first.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios", async (BuilderDb db, CancellationToken ct) =>
                (await db.Scenarios.ToListAsync(ct)).OrderByDescending(s => s.Updated).Select(ScenarioSummary.From))
            .WithTags(tag);
}
