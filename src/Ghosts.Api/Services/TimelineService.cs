﻿// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Services
{
    public interface ITimelineService
    {
        Task UpdateAsync(MachineUpdateViewModel machineUpdate, CancellationToken ct);
        Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdate, CancellationToken ct);
    }

    public class TimelineService : ITimelineService
    {
        private readonly ApplicationDbContext _context;

        public TimelineService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task UpdateAsync(MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            await _context.MachineUpdates.AddAsync(machineUpdate, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            var group = _context.Groups.Include(o => o.GroupMachines).FirstOrDefault(x => x.Id == groupId);

            if (group == null)
                return;

            foreach (var machineMapping in group.GroupMachines)
            {
                machineUpdate.MachineId = machineMapping.MachineId;
                await _context.MachineUpdates.AddAsync(machineUpdate, ct);
            }

            await _context.SaveChangesAsync(ct);
        }
    }
}