// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>GET /grayspace — where GraySpace is connected, by whom and since when, and whether it answers GET /server now.</summary>
public class StatusEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/grayspace", async (BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await db.GraySpaceConnections.FirstOrDefaultAsync(ct) is not { } connection)
                    return Results.Ok(GraySpaceView.From(null));
                try
                {
                    await grayspace.Send(connection.Url, connection.Token, HttpMethod.Get, "server", null, ct);
                    return Results.Ok(GraySpaceView.From(connection, answering: true));
                }
                catch (GraySpaceException e)
                {
                    return Results.Ok(GraySpaceView.From(connection, problem: e.Message));
                }
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
