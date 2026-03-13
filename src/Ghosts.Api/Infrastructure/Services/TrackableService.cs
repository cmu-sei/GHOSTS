// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface ITrackableService
    {
        Task<List<HistoryTrackable>> GetAsync(CancellationToken ct);
        Task<List<HistoryTrackable>> GetActivityByTrackableId(Guid trackableId, CancellationToken ct);
    }

    public class TrackableService(ApplicationDbContext context) : ITrackableService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<List<HistoryTrackable>> GetAsync(CancellationToken ct)
        {
            return await _context.HistoryTrackables.OrderByDescending(o => o.CreatedUtc).ToListAsync(ct);
        }

        public async Task<List<HistoryTrackable>> GetActivityByTrackableId(Guid trackableId, CancellationToken ct)
        {
            return await _context.HistoryTrackables.Where(o => o.TrackableId == trackableId).ToListAsync(ct);
        }
    }
}
