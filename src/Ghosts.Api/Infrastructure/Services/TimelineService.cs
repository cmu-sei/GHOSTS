// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Hubs;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace ghosts.api.Infrastructure.Services
{
    public interface ITimelineService
    {
        Task UpdateAsync(MachineUpdateViewModel machineUpdate, CancellationToken ct);
        Task StopAsync(Guid machineId, Guid timelineId, CancellationToken ct);
    }

    public class TimelineService(ApplicationDbContext context, IHubContext<ActivityHub> hub) : ITimelineService
    {
        public async Task UpdateAsync(MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            if (machineUpdateViewModel == null) return;

            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            context.MachineUpdates.Add(machineUpdate);

            var npc = await context.Npcs.FirstOrDefaultAsync(x=>x.MachineId == machineUpdate.MachineId, ct);
            if (npc != null)
            {
                await hub.Clients.All.SendAsync("show", 1, npc.Id, "activity",
                    machineUpdate.ToActivityPlainText(),
                    DateTime.Now.ToString(CultureInfo.InvariantCulture), CancellationToken.None);
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
    }
}
