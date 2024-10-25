// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Domain.Messages.MesssagesForServer;
using Microsoft.EntityFrameworkCore;

namespace ghosts.api.Infrastructure.Services
{
    public interface ISurveyService
    {
        Task<Survey> GetLatestAsync(Guid machineId, CancellationToken ct);
        Task<IEnumerable<Survey>> GetAllAsync(Guid machineId, CancellationToken ct);
    }

    public class SurveyService(ApplicationDbContext context) : ISurveyService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<Survey> GetLatestAsync(Guid machineId, CancellationToken ct)
        {
            return await _context.Surveys
                .Include(x => x.Drives)
                .Include("Interfaces.Bindings")
                .Include(x => x.Ports)
                .Include(x => x.Processes)
                .Include(x => x.EventLogs)
                .Include(x => x.LocalUsers)
                .Where(x => x.MachineId == machineId)
                .OrderByDescending(x => x.Created)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IEnumerable<Survey>> GetAllAsync(Guid machineId, CancellationToken ct)
        {
            return await _context.Surveys
                .Include(x => x.Drives)
                .Include("Interfaces.Bindings")
                .Include(x => x.Ports)
                .Include(x => x.Processes)
                .Include(x => x.EventLogs)
                .Include(x => x.LocalUsers)
                .Where(x => x.MachineId == machineId)
                .OrderByDescending(x => x.Created)
                .ToArrayAsync(ct);
        }
    }
}
