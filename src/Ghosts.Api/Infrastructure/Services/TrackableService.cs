// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ghosts.api.Infrastructure.Services
{
    public interface ITrackableService
    {
        Task<List<Trackable>> GetAsync(CancellationToken ct);
        Task<List<HistoryTrackable>> GetActivityByTrackableId(Guid trackableId, CancellationToken ct);
    }

    public class TrackableService(ApplicationDbContext context) : ITrackableService
    {
        private readonly ApplicationDbContext _context = context;

        public async Task<List<Trackable>> GetAsync(CancellationToken ct)
        {
            return await _context.Trackables.ToListAsync(ct);
        }

        public async Task<List<HistoryTrackable>> GetActivityByTrackableId(Guid trackableId, CancellationToken ct)
        {
            return await _context.HistoryTrackables.Where(o => o.TrackableId == trackableId).ToListAsync(ct);
        }
    }
}
