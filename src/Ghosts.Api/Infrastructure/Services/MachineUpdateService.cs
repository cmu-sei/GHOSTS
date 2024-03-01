// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using ghosts.api.Infrastructure.Models;
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
    }

    public class MachineUpdateService : IMachineUpdateService
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public MachineUpdateService(ApplicationDbContext context)
        {
            _context = context;
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