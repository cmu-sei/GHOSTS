// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace ghosts.api.Infrastructure.Services
{
    public interface IMachineUpdateService
    {
        Task<MachineUpdate> GetAsync(Guid id, string currentUsername, CancellationToken ct);

        Task<MachineUpdate> CreateAsync(MachineUpdate model, CancellationToken ct);
        //Task<Machine> UpdateAsync(Machine model, CancellationToken ct);
        Task<int> DeleteAsync(int id, Guid machineId, CancellationToken ct);
        
        Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdate, CancellationToken ct);

        Task<MachineUpdate> GetById(int updateId, CancellationToken ct);

        Task<IEnumerable<MachineUpdate>> GetByMachineId(Guid machineId, CancellationToken ct);

        Task<IEnumerable<MachineUpdate>> GetByStatus(StatusType status, CancellationToken ct);

        Task<IEnumerable<MachineUpdate>> GetByType(UpdateClientConfig.UpdateType type, CancellationToken ct);
    }

    public class MachineUpdateService : IMachineUpdateService
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public MachineUpdateService(ApplicationDbContext context)
        {
            _context = context;
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
                _context.MachineUpdates.Add(machineUpdate);
            }

            await _context.SaveChangesAsync(ct);
        }

        public async Task<MachineUpdate> GetAsync(Guid machineId, string currentUsername, CancellationToken ct)
        {
            var update = new MachineUpdate();
            if (!string.IsNullOrEmpty(currentUsername))
            {
                // if the username is there, but the machine id is not
                if (machineId == Guid.Empty)
                {
                    update = await _context.MachineUpdates
                        .FirstOrDefaultAsync(m => (m.Username.ToLower().StartsWith(currentUsername.ToLower())) && m.ActiveUtc < DateTime.UtcNow && m.Status == StatusType.Active, ct);
                }
                else // pick either
                {
                    update = await _context.MachineUpdates
                        .FirstOrDefaultAsync(
                            m => (m.MachineId == machineId || m.Username.ToLower().StartsWith(currentUsername.ToLower())) &&
                                 m.ActiveUtc < DateTime.UtcNow && m.Status == StatusType.Active, ct);
                }
            }
            else // just search for machine id
            {
                update = await _context.MachineUpdates
                    .FirstOrDefaultAsync(m => (m.MachineId == machineId) && m.ActiveUtc < DateTime.UtcNow && m.Status == StatusType.Active, ct);
            }

            return update;
        }

        public async Task<MachineUpdate> GetById(int updateId, CancellationToken ct)
        {
            return await _context.MachineUpdates.FirstOrDefaultAsync(x => x.Id == updateId, ct);
        }

        public async Task<IEnumerable<MachineUpdate>> GetByMachineId(Guid machineId, CancellationToken ct)
        {
            return await _context.MachineUpdates.Where(x => x.MachineId == machineId).ToListAsync(ct);
        }

        public async Task<IEnumerable<MachineUpdate>> GetByType(UpdateClientConfig.UpdateType type, CancellationToken ct)
        {
            return await _context.MachineUpdates.Where(x => x.Type == type).ToListAsync(ct);
        }
        
        public async Task<IEnumerable<MachineUpdate>> GetByStatus(StatusType status, CancellationToken ct)
        {
            return await _context.MachineUpdates.Where(x => x.Status == status).ToListAsync(ct);
        }

        public async Task<MachineUpdate> CreateAsync(MachineUpdate model, CancellationToken ct)
        {
            _context.MachineUpdates.Add(model);
            await _context.SaveChangesAsync(ct);
            return model;
        }

        public async Task<int> DeleteAsync(int id, Guid machineId, CancellationToken ct)
        {
            var model = await _context.MachineUpdates.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (model == null)
            {
                log.Error($"Machine update not found for id: {id}");
                throw new InvalidOperationException("Machine Update not found");
            }

            model.Status = StatusType.Deleted;
            model.MachineId = machineId;

            var operation = await _context.SaveChangesAsync(ct);
            if (operation >= 1) return id;
            
            log.Error($"Could not delete machine update: {operation}");
            throw new InvalidOperationException("Could not delete Machine Update");
        }
    }
}