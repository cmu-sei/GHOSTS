// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>DELETE /grayspace — an admin disconnects GraySpace and its token is forgotten. Nothing on GraySpace changes.</summary>
public class DisconnectEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/grayspace", async (BuilderDb db, CancellationToken ct) =>
            {
                if (await db.GraySpaceConnections.FirstOrDefaultAsync(ct) is { } connection)
                {
                    db.GraySpaceConnections.Remove(connection);
                    await db.SaveChangesAsync(ct);
                }
                return Results.NoContent();
            })
            .AddEndpointFilter<AdminOnly>().AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
