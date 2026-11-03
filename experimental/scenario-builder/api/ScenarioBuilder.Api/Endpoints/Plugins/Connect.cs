// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Plugins;

public record ConnectPlugin(string? Url);

/// <summary>PUT /plugins/{id} — an admin connects a plugin, or moves it, by its app's API address. Nothing is called yet.</summary>
public class ConnectEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/plugins/{id}", async (string id, ConnectPlugin body, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (Domain.Plugins.Find(id) is not { } plugin)
                    return ErrorResults.NotFound("PLUGIN_UNKNOWN", $"There is no plugin '{id}'. Plugins: {string.Join(", ", Domain.Plugins.All.Select(p => p.Id))}.");
                if (!Uri.TryCreate(body.Url?.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
                    return ErrorResults.BadRequest("URL_INVALID", $"Give {plugin.Name}'s API as a full web address, starting with http:// or https://.");

                var connection = await db.PluginConnections.FirstOrDefaultAsync(c => c.Plugin == id, ct);
                if (connection is null)
                    db.PluginConnections.Add(connection = new PluginConnection { Plugin = id });
                connection.Url = url.OriginalString;
                connection.ConnectedBy = Caller.From(http).User;
                connection.Connected = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(PluginView.From(plugin, connection));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
