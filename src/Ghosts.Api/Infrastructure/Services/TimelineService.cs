// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Newtonsoft.Json;

namespace ghosts.api.Infrastructure.Services
{
    public interface ITimelineService
    {
        Task UpdateAsync(MachineUpdateViewModel machineUpdate, CancellationToken ct);
        Task StopAsync(Guid machineId, Guid timelineId, CancellationToken ct);
    }

    public class TimelineService(ApplicationDbContext context) : ITimelineService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task UpdateAsync(MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            if (machineUpdateViewModel == null) return;
            
            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            _context.MachineUpdates.Add(machineUpdate);
            await _context.SaveChangesAsync(ct);
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

            var handlers = new List<TimelineHandler>
            {
                handler
            };
            
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

            _context.MachineUpdates.Add(o);
            await _context.SaveChangesAsync(ct);
        }
    }
}