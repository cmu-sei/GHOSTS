// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Ghosts.Api.Hubs;

public class ScenarioBuilderHub : Hub
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly ConnectionMapping<string> _connections = new();

    public override Task OnConnectedAsync()
    {
        var scenarioId = Context.GetHttpContext()?.Request.Query["scenarioId"].ToString() ?? "all";
        _connections.Add(scenarioId, Context.ConnectionId);
        _log.Debug($"ScenarioBuilder client connected: {Context.ConnectionId} for scenario {scenarioId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        var scenarioId = Context.GetHttpContext()?.Request.Query["scenarioId"].ToString() ?? "all";
        _connections.Remove(scenarioId, Context.ConnectionId);
        _log.Debug($"ScenarioBuilder client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }

    public static ConnectionMapping<string> GetConnections()
    {
        return _connections;
    }
}
