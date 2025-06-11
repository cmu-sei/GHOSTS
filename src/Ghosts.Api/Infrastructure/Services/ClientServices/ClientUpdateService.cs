// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Domain;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using NLog;

namespace Ghosts.Api.Infrastructure.Services.ClientServices;

public interface IClientUpdateService
{
    Task<(bool Success, UpdateClientConfig Update, int StatusCode, string Error)> GetUpdateAsync(HttpContext context, CancellationToken ct);
}

public class ClientUpdateService(
    IMachineService machineService,
    IMachineUpdateService updateService,
    IBackgroundQueue queue) : IClientUpdateService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public async Task<(bool Success, UpdateClientConfig Update, int StatusCode, string Error)> GetUpdateAsync(HttpContext context, CancellationToken ct)
    {
        try
        {
            var id = context.Request.Headers["ghosts-id"];
            _log.Trace($"Request by {id}");

            var findMachineResponse = await machineService.FindOrCreate(context, ct);
            if (!findMachineResponse.IsValid())
            {
                return (false, null, StatusCodes.Status401Unauthorized, findMachineResponse.Error);
            }

            var m = findMachineResponse.Machine;

            queue.Enqueue(new QueueEntry
            {
                Payload = new MachineQueueEntry
                {
                    Machine = m,
                    LogDump = null,
                    HistoryType = Machine.MachineHistoryItem.HistoryType.RequestedUpdates
                },
                Type = QueueEntry.Types.Machine
            });

            var u = await updateService.GetAsync(m.Id, m.CurrentUsername, ct);
            if (u == null)
            {
                return (false, null, StatusCodes.Status404NotFound, "No update available");
            }

            _log.Trace($"Update {u.Id} sent to {m.Id} {m.Name}({m.FQDN}) {u.Id} {u.Username} {u.Update}");

            var update = new UpdateClientConfig { Type = u.Type, Update = u.Update };

            await updateService.MarkAsDeletedAsync(u.Id, m.Id, ct);

            queue.Enqueue(new QueueEntry
            {
                Payload = new NotificationQueueEntry
                {
                    Type = NotificationQueueEntry.NotificationType.TimelineDelivered,
                    Payload = (JObject)JToken.FromObject(update)
                },
                Type = QueueEntry.Types.Notification
            });

            return (true, update, StatusCodes.Status200OK, string.Empty);
        }
        catch (Exception e)
        {
            _log.Error(e);
            return (false, null, StatusCodes.Status500InternalServerError, $"Exception occured: {e.Message}");
        }
    }
}
