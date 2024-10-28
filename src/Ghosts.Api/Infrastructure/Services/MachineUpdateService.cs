// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
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
        Task<int> MarkAsDeletedAsync(int id, Guid machineId, CancellationToken ct);

        Task UpdateGroupAsync(int groupId, MachineUpdateViewModel machineUpdate, CancellationToken ct);

        Task<MachineUpdate> GetById(int updateId, CancellationToken ct);

        Task<IEnumerable<MachineUpdate>> GetByMachineId(Guid machineId, CancellationToken ct);

        Task<IEnumerable<MachineUpdate>> GetByStatus(StatusType status, CancellationToken ct);

        Task<IEnumerable<MachineUpdate>> GetByType(UpdateClientConfig.UpdateType type, CancellationToken ct);
    }

    public class MachineUpdateService(ApplicationDbContext context) : IMachineUpdateService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

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
            if (machineId == Guid.Empty && string.IsNullOrEmpty(currentUsername))
                return new MachineUpdate();

            // Build the base query with conditions that are always true
            var query = _context.MachineUpdates
                .Where(m => m.ActiveUtc <= DateTime.UtcNow && m.Status == StatusType.Active);

            // Adjust the query based on the provided arguments
            if (machineId != Guid.Empty)
            {
                query = query.Where(m => m.MachineId == machineId);
            }
            else
            {
                if (!string.IsNullOrEmpty(currentUsername))
                {
                    var usernameLower = currentUsername.ToLower();
                    query = query.Where(m => m.Username.ToLower().StartsWith(usernameLower));
                }
            }

            var update = await query.FirstOrDefaultAsync(ct);
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
            var machineUpdate = await GetById(model.Id, ct);
            if (machineUpdate != null)
                return machineUpdate;

            model.Update.Id = Guid.NewGuid();

            _context.MachineUpdates.Add(model);
            await _context.SaveChangesAsync(ct);
            return model;
        }

        public async Task<int> MarkAsDeletedAsync(int id, Guid machineId, CancellationToken ct)
        {
            var model = await _context.MachineUpdates.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (model == null)
            {
                _log.Error($"Machine update not found for id: {id}");
                throw new InvalidOperationException("Machine Update not found");
            }

            model.Status = StatusType.Deleted;
            model.MachineId = machineId;
            _log.Info($"Marking machine update {id} as deleted.");

            var operation = await _context.SaveChangesAsync(ct);
            if (operation >= 1)
            {
                _log.Info($"Machine update {id} marked as deleted successfully.");
                return id;
            }

            _log.Error($"Could not mark machine update {id} as deleted: {operation}");
            throw new InvalidOperationException("Could not delete Machine Update");
        }
    }
}
