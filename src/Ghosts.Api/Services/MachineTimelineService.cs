// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Models;
using Ghosts.Domain;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Api.Services
{
    public interface IMachineTimelineService
    {
        Task<MachineTimeline> GetByMachineIdAsync(Guid id, CancellationToken ct);
        Task<MachineTimeline> CreateAsync(Machine model, Timeline timeline, CancellationToken ct);
        Task DeleteByMachineIdAsync(Guid model, CancellationToken ct);
    }

    public class MachineTimelineService : IMachineTimelineService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public MachineTimelineService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MachineTimeline> GetByMachineIdAsync(Guid id, CancellationToken ct)
        {
            return await _context.MachineTimelines.FirstOrDefaultAsync(x => x.MachineId == id, cancellationToken: ct);
        }

        public async Task<MachineTimeline> CreateAsync(Machine model, Timeline timeline, CancellationToken ct)
        {
            var t = new MachineTimeline {Timeline = JsonConvert.SerializeObject(timeline), MachineId = model.Id};

            await _context.MachineTimelines.AddAsync(t, ct);
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