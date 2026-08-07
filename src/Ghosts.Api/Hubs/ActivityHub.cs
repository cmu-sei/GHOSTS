// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Ghosts.Api.Hubs;

public class ActivityHub : Hub
{
    private static readonly ConnectionMapping<string> _connections = new();

    public override Task OnConnectedAsync()
    {
        _connections.Add("1", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _connections.Remove("1", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
