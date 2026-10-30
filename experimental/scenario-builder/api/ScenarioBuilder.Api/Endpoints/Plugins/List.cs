// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Plugins;

public record PluginView(string Id, string Name, string Description, bool Connected, string? Url, string? ConnectedBy, DateTime? Since)
{
    public static PluginView From(PluginInfo p, PluginConnection? c) =>
        new(p.Id, p.Name, p.Description, c is not null, c?.Url, c?.ConnectedBy, c?.Connected);
}

/// <summary>GET /plugins — every app a scenario can be sent to, and where each is connected. Open to everyone.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/plugins", async (BuilderDb db, CancellationToken ct) =>
            {
                var connections = await db.PluginConnections.ToDictionaryAsync(c => c.Plugin, ct);
                return Domain.Plugins.All.Select(p => PluginView.From(p, connections.GetValueOrDefault(p.Id)));
            })
            .WithTags(tag);
}
