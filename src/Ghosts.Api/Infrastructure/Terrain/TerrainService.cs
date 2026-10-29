// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>What to build. Either may be omitted: the enclave defaults to the scenario's terrain reference, the base to the enclave's.</summary>
public record BuildTerrainDto(string? BaseId, string? Enclave);

/// <summary>An execution's terrain as GHOSTS knows it, kept on the execution and refreshed by polling the provider.</summary>
public record ExecutionTerrainDto(
    int ExecutionId,
    string? Provider,
    string? BaseId,
    string? Enclave,
    string? InstanceId,
    string? Status,
    int Progress,
    List<string> Log,
    TerrainManifest? Manifest,
    string? Error,
    DateTime? UpdatedAt);

/// <summary>
/// Interim, until Player's API is settled: what GHOSTS can hand Crucible Player for a run, the participant consoles
/// in the ready manifest with the addresses and Proxmox ids a view needs. Teams and assignments are Player's.
/// </summary>
public record PlayerViewDto(int ExecutionId, string Name, string Enclave, List<PlayerConsoleDto> Consoles);

public record PlayerConsoleDto(string Name, string Segment, string RangeIp, string ManagementIp, string OsFamily, string ProxmoxNode, int Vmid);

public interface ITerrainService
{
    Task<ExecutionTerrainDto> GetAsync(int executionId, CancellationToken ct);
    Task<ExecutionTerrainDto> BuildAsync(int executionId, BuildTerrainDto dto, CancellationToken ct);
    Task<ExecutionTerrainDto> TeardownAsync(int executionId, CancellationToken ct);
    Task<PlayerViewDto> PlayerViewAsync(int executionId, CancellationToken ct);

    /// <summary>Refreshes every execution whose terrain is queued, building or tearing down. Returns how many changed.</summary>
    Task<int> PollAsync(CancellationToken ct);
}

/// <summary>
/// Builds, follows and tears down an execution's terrain through the provider. The build is keyed by the execution
/// (idempotency key execution-{id}), so a retried request is the same build, and only one build runs per execution.
/// </summary>
public class TerrainService(DbContext context, ITerrainProvider provider) : ITerrainService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly string[] InProgress = [TerrainStatus.Queued, TerrainStatus.Building, TerrainStatus.TearingDown];

    public async Task<ExecutionTerrainDto> GetAsync(int executionId, CancellationToken ct) =>
        Read(await ExecutionAsync(executionId, ct));

    public async Task<ExecutionTerrainDto> BuildAsync(int executionId, BuildTerrainDto dto, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        if (execution.TerrainStatus is TerrainStatus.Queued or TerrainStatus.Building or TerrainStatus.Ready or TerrainStatus.TearingDown)
            throw new InvalidOperationException($"Execution {executionId} already has terrain ({execution.TerrainStatus}). Tear it down first.");

        var terrain = ScenarioExtras.Read<ScenarioExtrasDto>(
            await context.Set<Scenario>().Where(s => s.Id == execution.ScenarioId).Select(s => s.Extras).FirstOrDefaultAsync(ct))?.Terrain;

        var enclave = Blank(dto.Enclave) ?? Blank(terrain?.Reference?.Enclave) ?? Blank(terrain?.Reference?.Slice)
            ?? throw new InvalidOperationException("No enclave to build: the request names none and the scenario's terrain reference has no enclave.");
        var baseId = Blank(dto.BaseId) ?? Blank(terrain?.Reference?.Base)
            ?? (await provider.ListEnclavesAsync(ct)).FirstOrDefault(e => e.Name == enclave)?.BaseId
            ?? throw new InvalidOperationException($"No base for enclave {enclave}: the request names none and the provider does not list it.");

        var expected = new TerrainExpectation(
            terrain?.Segments?.Select(s => new TerrainSegment(s.Name, s.Cidr, s.Description)).ToList(),
            terrain?.Hosts?.Select(h => new TerrainHost(h.Name, h.Segment, h.Os, h.Role, h.Services)).ToList());

        // A rebuild after a teardown or a failure is a new instance, so its key names the one it replaces.
        var previous = Read(execution).InstanceId;
        var key = string.IsNullOrEmpty(previous) ? $"execution-{executionId}" : $"execution-{executionId}-after-{previous}";
        var instance = await provider.CreateInstanceAsync(
            new CreateTerrainInstance(baseId, enclave, key, execution.Name, expected), ct);

        Write(execution, new ExecutionTerrainDto(executionId, terrain?.Reference?.Provider ?? "grayspace", baseId, enclave,
            instance.Id, instance.Status, instance.Progress, instance.Log ?? [], null, instance.Error, instance.UpdatedAt));
        AddEvent(execution, "TerrainRequested", $"Terrain requested: enclave {enclave} on base {baseId} (instance {instance.Id})");
        await context.SaveChangesAsync(ct);
        _log.Info($"[Execution {executionId}] Terrain requested: {enclave} on {baseId} → {instance.Id} ({instance.Status})");
        return Read(execution);
    }

    public async Task<ExecutionTerrainDto> TeardownAsync(int executionId, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var current = Read(execution);
        if (string.IsNullOrEmpty(current.InstanceId))
            throw new InvalidOperationException($"Execution {executionId} has no terrain to tear down.");

        var instance = await provider.DeleteInstanceAsync(current.InstanceId, ct);
        Write(execution, current with { Status = instance.Status, Progress = instance.Progress, Log = instance.Log ?? current.Log, UpdatedAt = instance.UpdatedAt });
        AddEvent(execution, "TerrainTeardown", $"Terrain teardown requested for instance {current.InstanceId}");
        await context.SaveChangesAsync(ct);
        return Read(execution);
    }

    public async Task<int> PollAsync(CancellationToken ct)
    {
        var executions = await context.Set<Execution>()
            .Where(e => e.TerrainStatus != null && InProgress.Contains(e.TerrainStatus))
            .ToListAsync(ct);

        var changed = 0;
        foreach (var execution in executions)
        {
            var current = Read(execution);
            if (string.IsNullOrEmpty(current.InstanceId)) continue;
            try
            {
                var instance = await provider.GetInstanceAsync(current.InstanceId, ct);
                var manifest = instance.Status == TerrainStatus.Ready && current.Status != TerrainStatus.Ready
                    ? await provider.GetManifestAsync(current.InstanceId, ct)
                    : null;

                // A teardown or a rebuild may have moved the terrain on while the provider answered; the next poll follows that.
                var saved = await context.Set<Execution>().AsNoTracking().Where(e => e.Id == execution.Id).Select(e => e.Terrain).FirstOrDefaultAsync(ct);
                if (saved != execution.Terrain) continue;

                var next = current with { Status = instance.Status, Progress = instance.Progress, Log = instance.Log ?? current.Log, Error = instance.Error, UpdatedAt = instance.UpdatedAt };
                if (manifest != null)
                {
                    next = next with { Manifest = manifest };
                    AddEvent(execution, "TerrainReady", $"Terrain ready: {next.Manifest?.Hosts?.Count ?? 0} hosts in instance {current.InstanceId}");
                }
                else if (instance.Status != current.Status && instance.Status is TerrainStatus.Failed or TerrainStatus.Deleted)
                {
                    AddEvent(execution, instance.Status == TerrainStatus.Failed ? "TerrainFailed" : "TerrainDeleted",
                        $"Terrain {instance.Status}: instance {current.InstanceId}{(instance.Error != null ? $" ({instance.Error})" : "")}");
                }

                if (next != current)
                {
                    Write(execution, next);
                    await context.SaveChangesAsync(ct);
                    changed++;
                }
            }
            catch (TerrainProviderException ex)
            {
                _log.Warn($"[Execution {execution.Id}] Terrain poll failed: {ex.Message}");
            }
        }

        return changed;
    }

    public async Task<PlayerViewDto> PlayerViewAsync(int executionId, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var terrain = Read(execution);
        if (terrain.Status != TerrainStatus.Ready || terrain.Manifest == null)
            throw new InvalidOperationException($"Execution {executionId} has no ready terrain yet ({terrain.Status ?? "not built"}).");

        var consoles = (terrain.Manifest.Hosts ?? []).Where(h => h.ParticipantWorkstation)
            .Select(h => new PlayerConsoleDto(h.Name, h.Segment, h.RangeIp, h.ManagementIp, h.OsFamily, h.ProxmoxNode, h.Vmid))
            .ToList();
        return new PlayerViewDto(execution.Id, execution.Name, terrain.Enclave, consoles);
    }

    private async Task<Execution> ExecutionAsync(int executionId, CancellationToken ct) =>
        await context.Set<Execution>().FirstOrDefaultAsync(e => e.Id == executionId, ct)
        ?? throw new InvalidOperationException("Execution not found");

    private static ExecutionTerrainDto Read(Execution execution) =>
        (string.IsNullOrEmpty(execution.Terrain) ? null : JsonSerializer.Deserialize<ExecutionTerrainDto>(execution.Terrain, GraySpaceTerrainProvider.Json))
        ?? new ExecutionTerrainDto(execution.Id, null, null, null, null, execution.TerrainStatus, 0, [], null, null, null);

    private static void Write(Execution execution, ExecutionTerrainDto terrain)
    {
        execution.Terrain = JsonSerializer.Serialize(terrain, GraySpaceTerrainProvider.Json);
        execution.TerrainStatus = terrain.Status;
    }

    private void AddEvent(Execution execution, string type, string description) =>
        context.Set<ExecutionEvent>().Add(new ExecutionEvent
        {
            ExecutionId = execution.Id,
            Timestamp = DateTime.UtcNow,
            EventType = type,
            Description = description,
            Data = "{}",
            Severity = type == "TerrainFailed" ? "Warning" : "Info"
        });

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
