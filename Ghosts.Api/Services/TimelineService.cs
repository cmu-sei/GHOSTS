// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

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
    public interface IMachineUpdateService
    {
        Task<MachineUpdate> GetAsync(Guid id, CancellationToken ct);
        //Task<Guid> CreateAsync(Machine model, CancellationToken ct);
        //Task<Machine> UpdateAsync(Machine model, CancellationToken ct);
        Task<int> DeleteAsync(int model, CancellationToken ct);
    }

    public class MachineUpdateService : IMachineUpdateService
    {
        private static Logger log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context;

        public MachineUpdateService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<MachineUpdate> GetAsync(Guid machineId, CancellationToken ct)
        {
            var update = await _context.MachineUpdates
                .FirstOrDefaultAsync(m => m.MachineId == machineId && m.ActiveUtc < DateTime.UtcNow && m.Status == StatusType.Active, ct);

            if (update == null)
                return null;

            var raw = new StringBuilder();

            var path = update.GetPath();

            if (File.Exists(path))
            {
                try
                {
                    foreach (var line in File.ReadAllLines(path))
                        raw.Append(line);
                }
                catch (Exception e)
                {
                    log.Error(e);
                    throw;
                }

                update.Update = raw.ToString();
            }
            
            return update;
        }

        public async Task<int> DeleteAsync(int id, CancellationToken ct)
        {
            var model = await _context.MachineUpdates.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (model == null)
            {
                log.Error($"Machine update not found for id: {id}");
                throw new InvalidOperationException("Machine Update not found");
            }

            model.Status = StatusType.Deleted;
            
            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                log.Error($"Could not delete machine update: {operation}");
                throw new InvalidOperationException("Could not delete Machine Update");
            }
            return id;
        }
    }
}
