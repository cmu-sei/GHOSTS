// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Plugins;

/// <summary>DELETE /plugins/{id} — an admin disconnects a plugin; Send waits for it again.</summary>
public class DisconnectEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/plugins/{id}", async (string id, BuilderDb db, CancellationToken ct) =>
            {
                if (Domain.Plugins.Find(id) is null)
                    return ErrorResults.NotFound("PLUGIN_UNKNOWN", $"There is no plugin '{id}'.");
                if (await db.PluginConnections.FirstOrDefaultAsync(c => c.Plugin == id, ct) is { } connection)
                {
                    db.PluginConnections.Remove(connection);
                    await db.SaveChangesAsync(ct);
                }
                return Results.NoContent();
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
