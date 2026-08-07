// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Ghosts.Api.Hubs;

/// <summary>
/// Standardizes the "show" activity envelope broadcast to the dynamic activities view.
/// Every emitter sends the same argument order: (eventId, npcId, type, message, time, executionId).
/// <paramref name="message"/> may be a plain string or a structured object
/// (e.g. { action, reasoning, handler, source, target, sentiment }).
/// <paramref name="executionId"/> is null when the activity is not attributable to an execution run.
/// </summary>
public static class ActivityHubExtensions
{
    public static Task Show(this IHubContext<ActivityHub> hub, long eventId, string npcId, string type,
        object message, int? executionId = null, CancellationToken ct = default)
    {
        return hub.Clients.All.SendAsync("show",
            eventId,
            npcId,
            type,
            message,
            DateTime.Now.ToString(CultureInfo.InvariantCulture),
            executionId,
            ct);
    }
}
