// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Services
{
    public interface IMachineGroupService
    {
        Task<List<Group>> GetAsync(string q, CancellationToken ct);
        Task<Group> GetAsync(int id, CancellationToken ct);
        Task<int> CreateAsync(Group model, CancellationToken ct);
        Task<Group> UpdateAsync(Group model, CancellationToken ct);
        Task<int> DeleteAsync(int model, CancellationToken ct);
        Task<List<HistoryTimeline>> GetActivity(int id, CancellationToken ct);
    }

    public class MachineGroupService : IMachineGroupService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public MachineGroupService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Group>> GetAsync(string q, CancellationToken ct)
        {
            var list = await _context.Groups.Include(o => o.GroupMachines).ToListAsync(ct);
            foreach (var group in list)
            foreach (var machineMapping in @group.GroupMachines)
            {
                var machine = await _context.Machines.FirstOrDefaultAsync(m => m.Id == machineMapping.MachineId && m.Status == StatusType.Active, ct);
                if (machine == null)
                    continue;
                @group.Machines.Add(machine);
            }

            return list;
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
            _context.Entry(model).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _log.Error("Machine group update exception", ex);
            }

            return model;
        }

        public async Task<int> DeleteAsync(int id, CancellationToken ct)
        {
            var machineGroup = await _context.Groups.FirstOrDefaultAsync(o => o.Id == id, ct);
            _context.Groups.Remove(machineGroup);
            await _context.SaveChangesAsync(ct);
            return id;
        }

        public async Task<List<HistoryTimeline>> GetActivity(int id, CancellationToken ct)
        {
            var machineGroup = await _context.Groups.Include(o => o.GroupMachines).FirstOrDefaultAsync(o => o.Id == id, ct);
            var machineIds = machineGroup.GroupMachines.Select(m => m.MachineId).ToList();

            try
            {
                return (from o in _context.HistoryTimeline where machineIds.Contains(o.MachineId) select o).Take(50).ToList();
            }
            catch (Exception e)
            {
                _log.Debug(e);
                throw;
            }
        }
    }
}