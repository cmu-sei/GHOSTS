// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>GET /grayspace — where GraySpace is connected, by whom and since when.</summary>
public class StatusEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/grayspace", async (BuilderDb db, CancellationToken ct) =>
                Results.Ok(GraySpaceView.From(await db.GraySpaceConnections.FirstOrDefaultAsync(ct))))
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
