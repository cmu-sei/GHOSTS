// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Data;
using Ghosts.Api.ViewModels;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Data;
using Ghosts.Api.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Services
{
    public interface ITimelineService
    {
        Task UpdateAsync(MachineUpdateViewModel machineUpdate, CancellationToken ct);
        Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdate, CancellationToken ct);
    }

    public class TimelineService : ITimelineService
        public TimelineService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task UpdateAsync(MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            _context.MachineUpdates.Add(machineUpdate);
            await _context.SaveChangesAsync(ct);
        }

        public async Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdateViewModel, CancellationToken ct)
        {
            var machineUpdate = machineUpdateViewModel.ToMachineUpdate();

            var group = this._context.Groups.Include(o => o.GroupMachines).FirstOrDefault(x => x.Id == groupId);

            if (group == null)
                return;
            
            foreach (var machineMapping in group.GroupMachines)
            {
                machineUpdate.MachineId = machineMapping.MachineId;
                _context.MachineUpdates.Add(machineUpdate);
            }

            await _context.SaveChangesAsync(ct);
        }
    }
}

