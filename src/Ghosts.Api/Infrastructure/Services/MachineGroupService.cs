// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IMachineGroupService
    {
        Task<List<Group>> GetAsync(string q, CancellationToken ct);
        Task<Group> GetAsync(int id, CancellationToken ct);
        Task<int> CreateAsync(Group model, CancellationToken ct);
        Task<Group> UpdateAsync(Group model, CancellationToken ct);
        Task<int> DeleteAsync(int model, CancellationToken ct);

        Task<Group> AddMachineToGroup(int groupId, Guid machineId, CancellationToken ct);
        Task<Group> RemoveMachineFromGroup(int groupId, Guid machineId, CancellationToken ct);
        Task<List<HistoryTimeline>> GetActivity(int id, int skip, int take, CancellationToken ct);
    }

    public class MachineGroupService(ApplicationDbContext context) : IMachineGroupService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<List<Group>> GetAsync(string q, CancellationToken ct)
        {
            return await _context.Groups.Include(o => o.GroupMachines).ToListAsync(ct);
        }

        public async Task<Group> GetAsync(int id, CancellationToken ct)
        {
            return await _context.Groups.Include(o => o.GroupMachines).FirstOrDefaultAsync(o => o.Id == id, ct);
        }

        public async Task<int> CreateAsync(Group model, CancellationToken ct)
        {
            _context.Groups.Add(model);
            await _context.SaveChangesAsync(ct);
            return model.Id;
        }

        public async Task<Group> UpdateAsync(Group model, CancellationToken ct)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var original = await _context.Groups
                .Include(g => g.GroupMachines)
                .FirstOrDefaultAsync(g => g.Id == model.Id, ct);

            if (original is null)
            {
                _log.Error($"No Group found with Id {model.Id}");
                return model;
            }

            // Remove old GroupMachines
            _context.GroupMachines.RemoveRange(original.GroupMachines);

            // Add new GroupMachines
            if (model.GroupMachines?.Count > 0)
            {
                foreach (var gm in model.GroupMachines.Where(gm => gm.MachineId != Guid.Empty))
                {
                    gm.GroupId = original.Id;
                    _context.GroupMachines.Add(gm);
                }
            }

            // Update scalar properties
            original.Name = model.Name;
            original.Status = model.Status;

            try
            {
                await _context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                _log.Error($"Failed to update Group with Id {model.Id}: {ex}");
                await transaction.RollbackAsync(ct);
            }

            return original;
        }


        public async Task<int> DeleteAsync(int id, CancellationToken ct)
        {
            var machineGroup = await _context.Groups.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (machineGroup != null)
            {
                _context.Groups.Remove(machineGroup);
                await _context.SaveChangesAsync(ct);
            }

            return id;
        }

        public async Task<Group> AddMachineToGroup(int groupId, Guid machineId, CancellationToken ct)
        {
            if (!_context.GroupMachines.Any(x => x.GroupId == groupId && x.MachineId == machineId))
            {
                _context.GroupMachines.Add(new GroupMachine { GroupId = groupId, MachineId = machineId });
                await _context.SaveChangesAsync(ct);

            }
            return await _context.Groups.Include(x => x.GroupMachines).FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken: ct);
        }

        public async Task<Group> RemoveMachineFromGroup(int groupId, Guid machineId, CancellationToken ct)
        {
            foreach (var r in
                     _context.GroupMachines.Where(x => x.GroupId == groupId && x.MachineId == machineId))
            {
                _context.GroupMachines.Remove(r);
            }
            await _context.SaveChangesAsync(ct);

            return await _context.Groups.Include(x => x.GroupMachines).FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken: ct);
        }

        public async Task<List<HistoryTimeline>> GetActivity(int id, int skip, int take, CancellationToken ct)
        {
            if (take < 1)
                take = 20;

            var machineGroup = await _context.Groups.Include(o => o.GroupMachines).FirstOrDefaultAsync(o => o.Id == id, ct);
            if (machineGroup == null) return new List<HistoryTimeline>();

            var machineIds = machineGroup.GroupMachines.Select(m => m.MachineId).ToList();
            if (machineIds.Count < 1) return new List<HistoryTimeline>();

            try
            {
                return (from o in _context.HistoryTimeline where machineIds.Contains(o.MachineId) select o)
                    .OrderByDescending(x => x.CreatedUtc).Skip(skip).Take(take).ToList();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

            return new List<HistoryTimeline>();
        }
    }
}
