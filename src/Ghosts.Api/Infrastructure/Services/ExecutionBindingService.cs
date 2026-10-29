// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Api.Infrastructure.Services;

public record BindingResultDto(int Bound, int Unbound, int Hosts);

public record NpcBindingDto(Guid NpcId, string Name, string Team, string HostName, Guid? MachineId);

/// <summary>Who sits where, for whoever creates the directory, mail and chat accounts on the hosts. No passwords.</summary>
public record NpcAccountDto(Guid NpcId, string Name, string Username, string Email, string Team, string HostName);

public interface IExecutionBindingService
{
    Task<List<NpcBindingDto>> ListAsync(int executionId, CancellationToken ct);
    Task<BindingResultDto> AutoBindAsync(int executionId, CancellationToken ct);
    Task SetHostAsync(int executionId, Guid npcId, string hostName, CancellationToken ct);
    Task<List<NpcAccountDto>> AccountsAsync(int executionId, CancellationToken ct);
}

/// <summary>
/// Binds a run's pool NPCs to hosts by name, one NPC per host. The hosts are GraySpace's GHOSTS-client hosts once the
/// terrain is ready, by the hostname they report, and the document's hosts before that; participant consoles get no NPC.
/// Each pool goes to the hosts GraySpace labelled with it, else its own hosts, else its segments' hosts, else any free
/// host. ExecutionDispatcher resolves a host name to a machine when a client with that exact name has registered.
/// </summary>
public class ExecutionBindingService(DbContext context) : IExecutionBindingService
{
    private record HostSeat(string Name, string Segment, string DocumentName = null, string Pool = null);

    public async Task<List<NpcBindingDto>> ListAsync(int executionId, CancellationToken ct)
    {
        var npcs = await context.Set<NpcRecord>().Where(n => n.ExecutionId == executionId).OrderBy(n => n.Team).ThenBy(n => n.CreatedUtc).ToListAsync(ct);
        return npcs.Select(n => new NpcBindingDto(n.Id, Name(n), n.Team, n.HostName, n.MachineId)).ToList();
    }

    public async Task<BindingResultDto> AutoBindAsync(int executionId, CancellationToken ct)
    {
        var execution = await context.Set<Execution>().FirstOrDefaultAsync(e => e.Id == executionId, ct)
                        ?? throw new InvalidOperationException("Execution not found");
        var seats = await SeatsAsync(execution, ct);
        var pools = await context.Set<ScenarioParameters>().Where(p => p.ScenarioId == execution.ScenarioId)
            .SelectMany(p => p.UserPools).OrderBy(p => p.Role).ToListAsync(ct);
        var npcs = await context.Set<NpcRecord>().Where(n => n.ExecutionId == executionId)
            .OrderBy(n => n.CreatedUtc).ThenBy(n => n.Id).ToListAsync(ct);

        var taken = npcs.Where(n => n.HostName != null).Select(n => n.HostName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var pool in pools)
        {
            var placement = ScenarioExtras.Read<PoolExtrasDto>(pool.Extras);
            var labelled = seats.Where(s => string.Equals(s.Pool, pool.Role, StringComparison.OrdinalIgnoreCase)).ToList();
            var candidates = labelled.Count > 0
                ? labelled
                : placement?.Hosts is { Count: > 0 } hosts
                ? seats.Where(s => hosts.Contains(s.DocumentName ?? s.Name, StringComparer.OrdinalIgnoreCase))
                : placement?.Segments is { Count: > 0 } segments
                    ? seats.Where(s => segments.Contains(s.Segment ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                    : seats;
            var free = new Queue<HostSeat>(candidates.Where(s => !taken.Contains(s.Name)));
            foreach (var npc in npcs.Where(n => n.HostName == null && string.Equals(n.Team, pool.Role, StringComparison.OrdinalIgnoreCase)))
            {
                if (free.Count == 0) break;
                var seat = free.Dequeue();
                npc.HostName = seat.Name;
                npc.MachineId = null;
                taken.Add(seat.Name);
            }
        }

        await context.SaveChangesAsync(ct);
        var bound = npcs.Count(n => n.HostName != null);
        return new BindingResultDto(bound, npcs.Count - bound, seats.Count);
    }

    public async Task SetHostAsync(int executionId, Guid npcId, string hostName, CancellationToken ct)
    {
        var npc = await context.Set<NpcRecord>().FirstOrDefaultAsync(n => n.Id == npcId && n.ExecutionId == executionId, ct)
                  ?? throw new InvalidOperationException("NPC not found in this execution");
        npc.HostName = string.IsNullOrWhiteSpace(hostName) ? null : hostName.Trim();
        npc.MachineId = null;
        await context.SaveChangesAsync(ct);
    }

    public async Task<List<NpcAccountDto>> AccountsAsync(int executionId, CancellationToken ct)
    {
        var npcs = await context.Set<NpcRecord>().Where(n => n.ExecutionId == executionId && n.HostName != null)
            .OrderBy(n => n.HostName).ToListAsync(ct);
        return npcs.Select(n =>
        {
            var email = n.NpcProfile?.Email ?? string.Empty;
            var username = email.Contains('@') ? email[..email.IndexOf('@')] : email;
            return new NpcAccountDto(n.Id, Name(n), username, email, n.Team, n.HostName);
        }).ToList();
    }

    /// <summary>The hosts NPCs can sit at: GraySpace's client hosts once ready, else the document's hosts; never a participant console.</summary>
    private async Task<List<HostSeat>> SeatsAsync(Execution execution, CancellationToken ct)
    {
        var extras = ScenarioExtras.Read<ScenarioExtrasDto>(
            await context.Set<Scenario>().Where(s => s.Id == execution.ScenarioId).Select(s => s.Extras).FirstOrDefaultAsync(ct));
        if (execution.TerrainStatus == TerrainStatus.Ready && TerrainService.Read(execution).Manifest is { Hosts.Count: > 0 } manifest)
            return TerrainService.Seats(manifest, extras?.Terrain).Where(s => s.Client)
                .Select(s => new HostSeat(s.Hostname, s.Segment, s.Name, s.Pool)).ToList();

        return (extras?.Terrain?.Hosts ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h.Name) && !IsConsole(h.Role))
            .Select(h => new HostSeat(h.Name, h.Segment)).ToList();
    }

    private static bool IsConsole(string role) =>
        (role ?? string.Empty).Contains("participant", StringComparison.OrdinalIgnoreCase)
        || (role ?? string.Empty).Contains("console", StringComparison.OrdinalIgnoreCase);

    private static string Name(NpcRecord n) =>
        n.NpcProfile?.Name != null ? $"{n.NpcProfile.Name.First} {n.NpcProfile.Name.Last}".Trim() : "Unknown";
}
