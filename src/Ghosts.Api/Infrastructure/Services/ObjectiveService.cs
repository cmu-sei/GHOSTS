using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

public interface IObjectiveService
{
    Task<List<Objective>> GetAllAsync(CancellationToken ct);
    Task<List<Objective>> GetByScenarioIdAsync(int scenarioId, CancellationToken ct);
    Task<Objective> GetByIdAsync(int id, CancellationToken ct);
    Task<Objective> CreateAsync(CreateObjectiveDto dto, CancellationToken ct);
    Task<Objective> UpdateAsync(int id, UpdateObjectiveDto dto, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public class ObjectiveService(ApplicationDbContext context) : IObjectiveService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationDbContext _context = context;

    public async Task<List<Objective>> GetAllAsync(CancellationToken ct)
    {
        var all = await _context.Objectives
            .OrderBy(o => o.SortOrder)
            .ThenByDescending(o => o.UpdatedAt)
            .ToListAsync(ct);

        return BuildTree(all);
    }

    public async Task<List<Objective>> GetByScenarioIdAsync(int scenarioId, CancellationToken ct)
    {
        var all = await _context.Objectives
            .Where(o => o.ScenarioId == scenarioId)
            .OrderBy(o => o.SortOrder)
            .ThenByDescending(o => o.UpdatedAt)
            .ToListAsync(ct);

        return BuildTree(all);
    }

    public async Task<Objective> GetByIdAsync(int id, CancellationToken ct)
    {
        var objective = await _context.Objectives
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (objective == null)
            throw new InvalidOperationException("Objective not found");

        var descendants = await GetDescendantsAsync(id, ct);
        AttachChildren(objective, descendants);

        return objective;
    }

    public async Task<Objective> CreateAsync(CreateObjectiveDto dto, CancellationToken ct)
    {
        var objective = new Objective
        {
            ParentId = dto.ParentId,
            ScenarioId = dto.ScenarioId,
            Name = dto.Name,
            Description = dto.Description,
            Type = dto.Type,
            Priority = dto.Priority,
            SuccessCriteria = dto.SuccessCriteria,
            Assigned = dto.Assigned ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Objectives.Add(objective);
        await _context.SaveChangesAsync(ct);

        _log.Info($"Created objective: {objective.Id} - {objective.Name}");
        return await GetByIdAsync(objective.Id, ct);
    }

    public async Task<Objective> UpdateAsync(int id, UpdateObjectiveDto dto, CancellationToken ct)
    {
        var objective = await _context.Objectives
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (objective == null)
            throw new InvalidOperationException("Objective not found");

        objective.Name = dto.Name;
        objective.Description = dto.Description;
        objective.Type = dto.Type;
        objective.Status = dto.Status;
        objective.Score = dto.Score;
        objective.Priority = dto.Priority;
        objective.SuccessCriteria = dto.SuccessCriteria;
        objective.Assigned = dto.Assigned ?? string.Empty;
        objective.SortOrder = dto.SortOrder;
        objective.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _log.Info($"Updated objective: {objective.Id} - {objective.Name}");
        return await GetByIdAsync(objective.Id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var objective = await _context.Objectives.FindAsync(id);
        if (objective == null)
            throw new InvalidOperationException("Objective not found");

        _context.Objectives.Remove(objective);
        await _context.SaveChangesAsync(ct);
        _log.Info($"Deleted objective: {id}");
    }

    private static List<Objective> BuildTree(List<Objective> flat)
    {
        var lookup = flat.ToLookup(o => o.ParentId);
        foreach (var obj in flat)
            obj.Children = lookup[obj.Id].ToList();
        return flat.Where(o => o.ParentId == null).ToList();
    }

    private async Task<List<Objective>> GetDescendantsAsync(int parentId, CancellationToken ct)
    {
        var all = await _context.Objectives
            .Where(o => o.ParentId != null)
            .OrderBy(o => o.SortOrder)
            .ToListAsync(ct);

        var result = new List<Objective>();
        var queue = new Queue<int>();
        queue.Enqueue(parentId);

        var lookup = all.ToLookup(o => o.ParentId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in lookup[current])
            {
                result.Add(child);
                queue.Enqueue(child.Id);
            }
        }

        return result;
    }

    private static void AttachChildren(Objective root, List<Objective> descendants)
    {
        var lookup = descendants.ToLookup(o => o.ParentId);
        root.Children = lookup[root.Id].ToList();
        foreach (var desc in descendants)
            desc.Children = lookup[desc.Id].ToList();
    }
}
