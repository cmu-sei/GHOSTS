// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Ghosts.Api.Hubs;

public class ExecutionHub : Hub
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly ConnectionMapping<string> _connections = new();

    public override Task OnConnectedAsync()
    {
        var executionId = Context.GetHttpContext()?.Request.Query["executionId"].ToString() ?? "all";
        _connections.Add(executionId, Context.ConnectionId);
        _log.Debug($"Execution client connected: {Context.ConnectionId} for execution {executionId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        var executionId = Context.GetHttpContext()?.Request.Query["executionId"].ToString() ?? "all";
        _connections.Remove(executionId, Context.ConnectionId);
        _log.Debug($"Execution client disconnected: {Context.ConnectionId}");
        return base.OnDisconnectedAsync(exception);
    }

    public static ConnectionMapping<string> GetConnections()
    {
        return _connections;
    }
}
