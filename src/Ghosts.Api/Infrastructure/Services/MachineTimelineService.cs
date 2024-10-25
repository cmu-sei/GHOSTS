// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Infrastructure.Services
{
    public interface IMachineTimelinesService
    {
        Task<IEnumerable<MachineTimeline>> GetByMachineIdAsync(Guid id, CancellationToken ct);
        Task<MachineTimeline> GetByMachineIdAndTimelineIdAsync(Guid id, Guid timelineId, CancellationToken ct);
        Task<MachineTimeline> CreateAsync(Machine model, Timeline timeline, CancellationToken ct);
        Task DeleteByMachineIdAsync(Guid model, CancellationToken ct);
    }

    public class MachineTimelinesService(ApplicationDbContext context) : IMachineTimelinesService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<IEnumerable<MachineTimeline>> GetByMachineIdAsync(Guid id, CancellationToken ct)
        {
            return await _context.MachineTimelines.Where(x => x.MachineId == id).ToListAsync(ct);
        }

        public async Task<MachineTimeline> GetByMachineIdAndTimelineIdAsync(Guid id, Guid timelineId, CancellationToken ct)
        {
            var timelines = await _context.MachineTimelines.Where(x => x.MachineId == id).ToListAsync(ct);
            foreach (var timeline in timelines)
            {
                var t = TimelineBuilder.StringToTimeline(timeline.Timeline);
                if (t.Id == timelineId)
                    return timeline;
            }

            return null;
        }

        public async Task<MachineTimeline> CreateAsync(Machine model, Timeline timeline, CancellationToken ct)
        {
            var t = new MachineTimeline { Timeline = JsonConvert.SerializeObject(timeline), MachineId = model.Id };

            _context.MachineTimelines.Add(t);
            _context.Entry(t).State = EntityState.Added;
            await _context.SaveChangesAsync(ct);

            return t;
        }

        public async Task DeleteByMachineIdAsync(Guid id, CancellationToken ct)
        {
            var o = await _context.MachineTimelines.FirstOrDefaultAsync(x => x.MachineId == id, ct);
            if (o == null)
            {
                _log.Error($"Machine timeline not found: {id}");
                throw new InvalidOperationException("Machine timeline not found");
            }

            _context.MachineTimelines.Remove(o);
            await _context.SaveChangesAsync(ct);
        }
    }
}
