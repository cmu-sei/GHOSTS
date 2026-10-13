// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface ITimelineService
    {
        Task UpdateAsync(MachineUpdateViewModel machineUpdate, CancellationToken ct);
        Task StopAsync(Guid machineId, Guid timelineId, CancellationToken ct);
        Task<(bool Found, Guid TimelineId, bool Pushed)> SendCommandsAsync(Guid machineId, HandlerType shell, IList<string> commands, CancellationToken ct);
    }

    public class TimelineService(ApplicationDbContext context, IHubContext<ActivityHub> hub, IClientHubService clientHubService) : ITimelineService
    {
        public async Task UpdateAsync(MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            if (machineUpdateViewModel == null) return;

            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            context.MachineUpdates.Add(machineUpdate);

            var npc = await context.Npcs.FirstOrDefaultAsync(x=>x.MachineId == machineUpdate.MachineId, ct);
            if (npc != null)
            {
                await hub.Show(1, npc.Id.ToString(), "activity",
                    new { action = machineUpdate.ToActivityPlainText(), reasoning = "", handler = "MachineUpdate" },
                    npc.ExecutionId, CancellationToken.None);
            }

            await context.SaveChangesAsync(ct);
        }

        public async Task StopAsync(Guid machineId, Guid timelineId, CancellationToken ct)
        {
            var timelineEvent = new TimelineEvent
            {
                Command = "stop"
            };

            var handler = new TimelineHandler
            {
                HandlerType = HandlerType.NpcSystem
            };
            handler.TimeLineEvents.Add(timelineEvent);

            var handlers = new List<TimelineHandler>();
            handlers.Add(handler);

            var timeline = new Timeline
            {
                Id = timelineId,
                Status = Timeline.TimelineStatus.Run,
                TimeLineHandlers = handlers
            };

            var o = new MachineUpdate
            {
                Status = StatusType.Active,
                Update = timeline,
                ActiveUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                MachineId = machineId,
                Type = UpdateClientConfig.UpdateType.TimelinePartial
            };

            context.MachineUpdates.Add(o);
            await context.SaveChangesAsync(ct);
        }

        public async Task<(bool Found, Guid TimelineId, bool Pushed)> SendCommandsAsync(Guid machineId, HandlerType shell, IList<string> commands, CancellationToken ct)
        {
            if (!await context.Machines.AnyAsync(x => x.Id == machineId, ct))
                return (false, Guid.Empty, false);

            var handler = new TimelineHandler
            {
                HandlerType = shell,
                Loop = false,
                TimeLineEvents = commands.Select(c => new TimelineEvent { Command = c }).ToList()
            };

            var timeline = new Timeline
            {
                Id = Guid.NewGuid(),
                Status = Timeline.TimelineStatus.Run,
                TimeLineHandlers = [handler]
            };

            var o = new MachineUpdate
            {
                Status = StatusType.Active,
                Update = timeline,
                MachineId = machineId,
                Type = UpdateClientConfig.UpdateType.TimelinePartial
            };

            // Push over the client's socket if it is connected; otherwise queue it for the client's next poll
            if (await clientHubService.SendUpdate(machineId, o))
                return (true, timeline.Id, true);

            context.MachineUpdates.Add(o);
            await context.SaveChangesAsync(ct);
            return (true, timeline.Id, false);
        }
    }
}
