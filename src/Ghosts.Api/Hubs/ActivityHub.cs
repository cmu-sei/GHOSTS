// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Ghosts.Animator.Extensions;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace Ghosts.Api.Hubs;

public class ActivityHub : Hub
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

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

    public async Task Show(int eventId, string npcId, string type, string message, string time)
    {
        _log.Debug("Processing update");

        //do some saving

        foreach (var connectionId in _connections.GetConnections("1"))
        {
            var types = new[] { "social", "belief", "knowledge", "relationship" };
            var t = types.RandomFromStringArray();

            await Clients.Client(connectionId).SendAsync("show", eventId,
                npcId,
                t,
                Faker.Lorem.Sentence(),
                DateTime.Now.ToString(CultureInfo.InvariantCulture));
        }
    }
}
