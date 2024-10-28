// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Infrastructure.Models;
using Ghosts.Api;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace ghosts.api.Infrastructure.Services
{
    public interface IMachineService
    {
        Task<List<Machine>> GetAsync(string q, CancellationToken ct);
        Task<Machine> GetByIdAsync(Guid id, CancellationToken ct);

        Task<FindMachineResponse> FindOrCreate(HttpContext httpContext, CancellationToken ct);
        List<MachineListItem> GetList();
        Task<Guid> CreateAsync(Machine model, CancellationToken ct);
        Task<Machine> UpdateAsync(Machine model, CancellationToken ct);
        Task<Guid> DeleteAsync(Guid model, CancellationToken ct);
        Task<List<HistoryHealth>> GetMachineHistoryHealth(Guid model, CancellationToken ct);
        Task<List<Machine.MachineHistoryItem>> GetMachineHistory(Guid model, CancellationToken ct);
        Task<List<HistoryTimeline>> GetActivity(Guid id, int skip, int take, CancellationToken ct);
    }

    public class MachineService(ApplicationDbContext context) : IMachineService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;
        private readonly int _lookBack = Program.ApplicationSettings.LookbackRecords;

        public async Task<List<Machine>> GetAsync(string q, CancellationToken ct)
        {
            var list = await _context.Machines.Where(o => o.Status == StatusType.Active).OrderByDescending(o => o.LastReportedUtc).ToListAsync(ct);
            foreach (var item in list)
            {
                item.HistoryHealth = null;
                item.HistoryTimeline = null;
                item.HistoryTrackables = null;
                item.History = null;
            }

            return list;
        }

        public async Task<FindMachineResponse> FindOrCreate(HttpContext httpContext, CancellationToken ct)
        {
            var machineResponse = new FindMachineResponse();
            var m = WebRequestReader.GetMachine(httpContext);

            if (m.Id == Guid.Empty)
            {
                m = await FindByValue(WebRequestReader.GetMachine(httpContext), ct);
            }

            if (m is null || !m.IsValid())
            {
                m = WebRequestReader.GetMachine(httpContext);

                m.History.Add(new Machine.MachineHistoryItem { Type = Machine.MachineHistoryItem.HistoryType.Created });
                await CreateAsync(m, ct);

                if (!m.IsValid())
                {
                    machineResponse.Error = "Invalid machine request";
                }
                else
                {
                    m.History.Add(new Machine.MachineHistoryItem { Type = Machine.MachineHistoryItem.HistoryType.RequestedId });
                    await UpdateAsync(m, ct);

                    machineResponse.Machine = m;
                }
            }
            else
            {
                machineResponse.Machine = m;
            }

            return machineResponse;
        }

        private async Task<Machine> FindByValue(Machine machine, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(Program.ApplicationSettings.MatchMachinesBy))
            {
                return await _context.Machines.FirstOrDefaultAsync(o => o.Name.ToLower().Contains(machine.Name)
                                                                        || o.FQDN.ToLower().Contains(machine.FQDN.ToLower())
                                                                        || o.Host.ToLower().Contains(machine.Host.ToLower())
                                                                        || o.ResolvedHost.ToLower().Contains(machine.ResolvedHost.ToLower()), ct);
            }
            else
            {
                return Program.ApplicationSettings.MatchMachinesBy.ToLower() switch
                {
                    "name" => await _context.Machines.FirstOrDefaultAsync(o => o.Name.ToLower().Contains(machine.Name.ToLower()), ct),
                    "fqdn" => await _context.Machines.FirstOrDefaultAsync(o => o.FQDN.ToLower().Contains(machine.FQDN.ToLower()), ct),
                    "host" => await _context.Machines.FirstOrDefaultAsync(o => o.Host.ToLower().Contains(machine.Host.ToLower()), ct),
                    "resolvedhost" => await _context.Machines.FirstOrDefaultAsync(o => o.ResolvedHost.ToLower().Contains(machine.ResolvedHost.ToLower()), ct),
                    _ => await _context.Machines.FirstOrDefaultAsync(o => o.Name.ToLower().Contains(machine.Name.ToLower()), ct)
                };
            }
        }

        public List<MachineListItem> GetList()
        {
            var items = new List<MachineListItem>();
            var q = from r in _context.Machines
                    select new
                    {
                        r.Id,
                        r.Name
                    };
            foreach (var item in q)
                items.Add(new MachineListItem
                {
                    Id = item.Id,
                    Name = item.Name
                });
            return items;
        }

        public async Task<Machine> GetByIdAsync(Guid id, CancellationToken ct)
        {
            var machine = await _context.Machines.FirstOrDefaultAsync(o => o.Id == id, ct);

            if (machine == null)
                return null;

            machine.AddHistoryMachine(await _context.HistoryMachine.Where(o => o.MachineId == machine.Id)
                .OrderByDescending(o => o.CreatedUtc).Take(_lookBack).ToListAsync(ct));
            machine.AddHistoryHealth(await _context.HistoryHealth.Where(o => o.MachineId == machine.Id)
                .OrderByDescending(o => o.CreatedUtc).Take(_lookBack).ToListAsync(ct));
            machine.AddHistoryTimeline(await _context.HistoryTimeline.Where(o => o.MachineId == machine.Id)
                .OrderByDescending(o => o.CreatedUtc).Take(_lookBack).ToListAsync(ct));

            return machine;
        }

        public async Task<Guid> CreateAsync(Machine model, CancellationToken ct)
        {
            model.StatusUp = Machine.UpDownStatus.Up;
            model.LastReportedUtc = DateTime.UtcNow;

            var ex = await _context.Machines.FirstOrDefaultAsync(o => o.Id == model.Id, ct);
            if (ex != null) return ex.Id;

            if (model.Id == Guid.Empty)
                model.Id = Guid.NewGuid();
            _context.Machines.Add(model);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Count not create machine: {operation}");
                throw new InvalidOperationException("Could not create Machine");
            }

            AddToGroups(model, ct);

            return model.Id;
        }

        public async Task<Machine> UpdateAsync(Machine model, CancellationToken ct)
        {
            model.StatusUp = Machine.UpDownStatus.Up;
            model.LastReportedUtc = DateTime.UtcNow;

            var ex = await _context.Machines.FirstOrDefaultAsync(o => o.Id == model.Id, ct);
            if (ex == null)
            {
                _log.Error("Machine not found");
                throw new InvalidOperationException("Machine not found");
            }

            _context.Entry(model).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException e)
            {
                _log.Error($"MachineService Update save error {e}");
            }

            try
            {
                _context.Machines.Update(ex);

                var operation = await _context.SaveChangesAsync(ct);
                if (operation < 1)
                {
                    _log.Error($"Count not update machine: {operation}");
                    throw new InvalidOperationException($"Could not update Machine, operation was {operation}");
                }
            }
            catch (Exception e)
            {
                _log.Error(e, "MachineService Update machine error");
            }

            AddToGroups(model, ct);

            return model;
        }

        public async Task<Guid> DeleteAsync(Guid id, CancellationToken ct)
        {
            var a = await _context.Machines.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (a == null)
            {
                _log.Error($"Machine not found: {id}");
                throw new InvalidOperationException("Machine not found");
            }

            a.Status = StatusType.Deleted;
            _context.Entry(a).State = EntityState.Modified;

            var operation = await _context.SaveChangesAsync(ct);
            if (operation >= 1) return id;

            _log.Error($"Could not delete machine: {operation}");
            throw new InvalidOperationException("Could not delete Machine");
        }

        public async Task<List<HistoryHealth>> GetMachineHistoryHealth(Guid id, CancellationToken ct)
        {
            return await _context.HistoryHealth
                .Where(o => o.MachineId == id)
                .OrderByDescending(o => o.CreatedUtc)
                .ToListAsync(ct);
        }

        public async Task<List<Machine.MachineHistoryItem>> GetMachineHistory(Guid id, CancellationToken ct)
        {
            return await _context.HistoryMachine.Where(o => o.MachineId == id).ToListAsync(ct);
        }

        public async Task<List<HistoryTimeline>> GetActivity(Guid id, int skip, int take, CancellationToken ct)
        {
            if (take < 1)
                take = 25;

            var machine = await _context.Machines.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (machine == null)
            {
                _log.Error($"Machine not found: {id}");
                throw new InvalidOperationException("Machine not found");
            }

            try
            {
                return _context.HistoryTimeline.Where(o => o.MachineId == id).OrderByDescending(o => o.CreatedUtc).Skip(skip).Take(take).ToList();
            }
            catch (Exception e)
            {
                _log.Debug(e);
                throw;
            }
        }


        private void AddToGroups(Machine model, CancellationToken ct)
        {
            var groups = GroupNames.GetGroupNames(model);

            foreach (var group in groups)
            {
                var existingGroup = _context.Groups.Include(o => o.GroupMachines).FirstOrDefaultAsync(o => o.Name.Equals(group), ct).Result;

                if (existingGroup != null && existingGroup.Name == group)
                {
                    if (existingGroup.GroupMachines.Any(o => o.MachineId.Equals(model.Id))) continue;

                    existingGroup.GroupMachines.Add(new GroupMachine { GroupId = existingGroup.Id, MachineId = model.Id });
                    _context.SaveChanges();
                }
                else
                {
                    var newGroup = new Group { Name = @group, Status = StatusType.Active };
                    newGroup.GroupMachines.Add(new GroupMachine { GroupId = newGroup.Id, MachineId = model.Id });
                    _context.Groups.Add(newGroup);
                    _context.SaveChanges();
                }
            }
        }
    }
}
