// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NLog;

namespace Ghosts.Api.Infrastructure.Terrain;

/// <summary>What to build. Either may be omitted: the base defaults to the scenario's terrain reference, the enclave to its reference or slug.</summary>
public record BuildTerrainDto(string? BaseId, string? Enclave);

/// <summary>What GraySpace built for an execution: its sites, hosts, DNS records and certificates.</summary>
public record TerrainManifest(List<ManifestSite> Sites, List<ManifestHost> Hosts, List<ManifestRecord> Dns, List<ManifestCertificate> Certificates);

/// <summary>
/// An execution's terrain as GHOSTS knows it, kept on the execution and refreshed from GraySpace. BaseId is the
/// deployed base version, Enclave the stored enclave's slug and Version its pinned version; InstanceId is GraySpace's
/// instance (g{executionId}) and JobId its current job. Findings are GraySpace's, Notes what GHOSTS could not carry
/// over from the scenario.
/// </summary>
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
    DateTime? UpdatedAt,
    string? Version = null,
    string? JobId = null,
    string? Stage = null,
    List<Finding>? Findings = null,
    List<string>? Notes = null);

/// <summary>
/// Interim, until Player's API is settled: what GHOSTS can hand Crucible Player for a run, the participant consoles
/// GraySpace built (hosts labelled ghosts.console) and their addresses. Teams and assignments are Player's.
/// </summary>
public record PlayerViewDto(int ExecutionId, string Name, string? Enclave, List<PlayerConsoleDto> Consoles);

/// <summary>
/// A host GraySpace built, in the scenario document's terms: the hostname it reports, its own name, its segment as the
/// document names it, its pool label, and whether it runs a GHOSTS client that an NPC can sit at (not a console).
/// </summary>
public record TerrainSeat(string Hostname, string Name, string Segment, string Pool, bool Client);

public record PlayerConsoleDto(string Name, string Hostname, string? Segment, string Ip, string? MgmtIp, string Image);

public interface ITerrainService
{
    Task<ExecutionTerrainDto> GetAsync(int executionId, CancellationToken ct);
    Task<ExecutionTerrainDto> BuildAsync(int executionId, BuildTerrainDto dto, CancellationToken ct);
    Task<ExecutionTerrainDto> TeardownAsync(int executionId, CancellationToken ct);
    Task<PlayerViewDto> PlayerViewAsync(int executionId, CancellationToken ct);

    /// <summary>GraySpace's map layer as GeoJSON, with this execution's enclave.</summary>
    Task<string> MapAsync(int executionId, CancellationToken ct);

    /// <summary>Refreshes every execution whose terrain is queued, building or tearing down. Returns how many changed.</summary>
    Task<int> PollAsync(CancellationToken ct);
}

/// <summary>
/// Builds, follows and tears down an execution's terrain through GraySpace Terrain API 1.3.0. A build checks the
/// deployed base, turns the scenario's terrain and pools into a slice spec (EnclaveSliceMapper), stores it, and creates
/// an instance with the prefix g{executionId}, so one enclave can run once per execution. Errors in GraySpace's findings
/// refuse the build and stay on the terrain. A failed build is retried from the stage that failed; after a teardown a
/// build creates a new instance.
/// </summary>
public class TerrainService(DbContext context, IGraySpace graySpace, IOptions<TerrainProviderOptions> settings) : ITerrainService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly string[] InProgress = [TerrainStatus.Queued, TerrainStatus.Building, TerrainStatus.TearingDown];
    private const int LogLines = 200;

    /// <summary>How an execution's terrain is kept in its jsonb column.</summary>
    public static readonly JsonSerializerOptions Stored = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string InstanceIdFor(int executionId) => $"g{executionId}";

    public async Task<ExecutionTerrainDto> GetAsync(int executionId, CancellationToken ct) =>
        Read(await ExecutionAsync(executionId, ct));

    public async Task<ExecutionTerrainDto> BuildAsync(int executionId, BuildTerrainDto dto, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        if (execution.TerrainStatus is TerrainStatus.Queued or TerrainStatus.Building or TerrainStatus.Ready or TerrainStatus.TearingDown)
            throw new InvalidOperationException($"Execution {executionId} already has terrain ({execution.TerrainStatus}). Tear it down first.");

        var scenario = await context.Set<Scenario>().AsNoTracking().FirstAsync(s => s.Id == execution.ScenarioId, ct);
        var extras = ScenarioExtras.Read<ScenarioExtrasDto>(scenario.Extras);
        var reference = extras?.Terrain?.Reference;

        var deployed = await graySpace.GetBaseAsync(ct);
        if (deployed.Status == "not_deployed")
            throw new InvalidOperationException("GraySpace has no base deployed; deploy one before building terrain.");
        // GraySpace names the base only when it knows its version; a base whose CSVs came from elsewhere has no name.
        var baseName = deployed.Version?.Name;
        var wanted = Blank(dto.BaseId) ?? Blank(reference?.Base);
        if (wanted != null && baseName != null && wanted != baseName)
            throw new InvalidOperationException($"The scenario names base {wanted}, but GraySpace runs {baseName}.");

        var caps = await graySpace.GetCapabilitiesAsync(ct);
        if (!caps.Jobs)
            throw new InvalidOperationException($"GraySpace can't build terrain here: {caps.JobsUnavailableReason ?? "its jobs are unavailable"}.");

        // A failed teardown leaves the instance failed; building would only retry the teardown.
        var previous = Read(execution);
        if (previous.Status == TerrainStatus.Failed && previous.JobId != null
            && (await graySpace.GetJobAsync(previous.JobId, ct)).Kind == "destroy_instance")
            throw new InvalidOperationException("The last teardown failed; tear the terrain down again before building.");

        var pools = await context.Set<ScenarioParameters>().AsNoTracking().Where(p => p.ScenarioId == scenario.Id)
            .SelectMany(p => p.UserPools).ToListAsync(ct);
        var input = new EnclaveInput(Blank(dto.Enclave) ?? Blank(reference?.Enclave) ?? Blank(reference?.Slice) ?? Blank(extras?.Slug) ?? scenario.Name,
            scenario.Name, scenario.Description, extras?.Terrain,
            pools.Select(p =>
            {
                var placement = ScenarioExtras.Read<PoolExtrasDto>(p.Extras);
                return new PoolPlacement(p.Role, p.Count, placement?.Segments ?? [], placement?.Hosts ?? []);
            }).ToList());
        var mapping = EnclaveSliceMapper.Map(input, caps, settings.Value);

        var put = await graySpace.PutSliceAsync(mapping.Spec, ct);
        var current = Read(execution);
        var errors = put.Findings.Where(f => f.Severity == "error").ToList();
        var terrain = new ExecutionTerrainDto(executionId, "grayspace", baseName, mapping.Spec.Slug, null, null, 0, [], null, null,
            DateTime.UtcNow, put.Version, null, null, put.Findings, mapping.Notes);
        if (errors.Count > 0)
            return await RefuseAsync(execution, terrain, current, errors, ct);

        JobAccepted accepted;
        if (current.Status == TerrainStatus.Failed && current.JobId != null && current.InstanceId != null)
        {
            if (current.Version != put.Version)
                throw new InvalidOperationException("The enclave changed since the failed build; tear the terrain down, then build again.");
            accepted = await graySpace.RetryJobAsync(current.JobId, ct);
        }
        else
        {
            try
            {
                accepted = await graySpace.CreateInstanceAsync(new CreateInstanceRequest(mapping.Spec.Slug, put.Version, InstanceIdFor(executionId)), ct);
            }
            catch (TerrainProviderException ex) when (ex.Findings is { Count: > 0 })
            {
                // GraySpace checks again at create, with this execution's prefix and the node's capacity.
                return await RefuseAsync(execution, terrain with { Findings = [.. put.Findings, .. ex.Findings] }, current, ex.Findings, ct);
            }
        }

        terrain = terrain with { InstanceId = accepted.Instance ?? InstanceIdFor(executionId), JobId = accepted.Job.Id, Status = TerrainStatus.Queued };
        Write(execution, terrain);
        AddEvent(execution, "TerrainRequested", $"Terrain requested: enclave {terrain.Enclave} ({put.Version}) on {terrain.BaseId}, instance {terrain.InstanceId}, job {terrain.JobId}");
        await context.SaveChangesAsync(ct);
        _log.Info($"[Execution {executionId}] Terrain requested: {terrain.Enclave} on {terrain.BaseId} → {terrain.InstanceId} ({accepted.Job.Kind} {accepted.Job.Id})");
        return Read(execution);
    }

    public async Task<ExecutionTerrainDto> TeardownAsync(int executionId, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        var current = Read(execution);
        if (string.IsNullOrEmpty(current.InstanceId) || current.Status == TerrainStatus.Deleted)
            throw new InvalidOperationException($"Execution {executionId} has no terrain to tear down.");

        JobAccepted accepted;
        try
        {
            accepted = await graySpace.DestroyInstanceAsync(current.InstanceId, ct);
        }
        catch (TerrainProviderException ex) when (ex.Status == 404 || (ex.Status == 409 && ex.Detail.Contains("already destroyed")))
        {
            // Destroyed already, or forgotten: from GraySpace's UI, its CLI, or a reinstall.
            Write(execution, current with { Status = TerrainStatus.Deleted, Error = $"GraySpace no longer has instance {current.InstanceId}: {ex.Detail}", UpdatedAt = DateTime.UtcNow });
            AddEvent(execution, "TerrainDeleted", $"Terrain deleted: {ex.Detail}");
            await context.SaveChangesAsync(ct);
            return Read(execution);
        }
        catch (TerrainProviderException ex) when (ex.Status == 409 && ex.Detail.Contains("being destroyed"))
        {
            accepted = new JobAccepted(await graySpace.GetJobAsync((await graySpace.GetInstanceAsync(current.InstanceId, ct)).Jobs[^1], ct), current.InstanceId);
        }
        Write(execution, current with { Status = TerrainStatus.TearingDown, JobId = accepted.Job.Id, Stage = accepted.Job.Stage, Progress = 0, UpdatedAt = DateTime.UtcNow });
        AddEvent(execution, "TerrainTeardown", $"Terrain teardown requested for instance {current.InstanceId} (job {accepted.Job.Id})");
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
            if (string.IsNullOrEmpty(current.InstanceId) || string.IsNullOrEmpty(current.JobId)) continue;
            try
            {
                var job = await graySpace.GetJobAsync(current.JobId, ct);
                var log = await graySpace.GetJobLogAsync(current.JobId, ct);
                var instance = await graySpace.GetInstanceAsync(current.InstanceId, ct);

                // A teardown or a rebuild may have moved the terrain on while GraySpace answered; the next poll follows that.
                var saved = await context.Set<Execution>().AsNoTracking().Where(e => e.Id == execution.Id).Select(e => e.Terrain).FirstOrDefaultAsync(ct);
                if (saved != execution.Terrain) continue;

                var status = StatusOf(job, instance);
                var next = current with
                {
                    Status = status,
                    Stage = job.Stage,
                    Progress = Progress(job),
                    Log = (log ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(LogLines).ToList(),
                    Error = instance.Error ?? job.Error,
                    Manifest = status == TerrainStatus.Ready ? new TerrainManifest(instance.Sites, instance.Hosts, instance.Dns, instance.Certificates) : current.Manifest,
                    UpdatedAt = DateTime.UtcNow
                };
                if (status != current.Status)
                {
                    if (status == TerrainStatus.Ready)
                        AddEvent(execution, "TerrainReady", $"Terrain ready: {instance.Hosts.Count} hosts in instance {current.InstanceId}");
                    else if (status is TerrainStatus.Failed or TerrainStatus.Deleted)
                        AddEvent(execution, status == TerrainStatus.Failed ? "TerrainFailed" : "TerrainDeleted",
                            $"Terrain {status}: instance {current.InstanceId}{(next.Error != null ? $" ({next.Error})" : "")}");
                }

                if (JsonSerializer.Serialize(next, Stored) != JsonSerializer.Serialize(current, Stored))
                {
                    Write(execution, next);
                    await context.SaveChangesAsync(ct);
                    changed++;
                }
            }
            catch (TerrainProviderException ex) when (ex.Status == 404)
            {
                Write(execution, current with { Status = TerrainStatus.Deleted, Error = $"GraySpace no longer has instance {current.InstanceId}: {ex.Detail}", UpdatedAt = DateTime.UtcNow });
                AddEvent(execution, "TerrainDeleted", $"Terrain deleted: GraySpace no longer has instance {current.InstanceId}");
                await context.SaveChangesAsync(ct);
                changed++;
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

        var consoles = terrain.Manifest.Hosts
            .Where(h => h.Labels?.GetValueOrDefault(EnclaveSliceMapper.ConsoleLabel) == "true")
            .Select(h => new PlayerConsoleDto(h.Name, h.Hostname, h.Segment, h.Ip, h.MgmtIp, h.Image))
            .ToList();
        return new PlayerViewDto(execution.Id, execution.Name, terrain.Enclave, consoles);
    }

    public async Task<string> MapAsync(int executionId, CancellationToken ct)
    {
        var execution = await ExecutionAsync(executionId, ct);
        return await graySpace.GetMapAsync(Read(execution).InstanceId ?? InstanceIdFor(executionId), ct);
    }

    /// <summary>The manifest's hosts in the document's terms. GraySpace names segments as EnclaveSliceMapper did, so they map back.</summary>
    public static List<TerrainSeat> Seats(TerrainManifest manifest, TerrainExtrasDto? terrain)
    {
        var segmentNames = (terrain?.Segments ?? []).Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .GroupBy(s => EnclaveSliceMapper.Name(s.Name, 24)).ToDictionary(g => g.Key, g => g.First().Name);
        return (manifest?.Hosts ?? []).Select(h => new TerrainSeat(h.Hostname, h.Name,
            h.Segment != null && segmentNames.TryGetValue(h.Segment, out var named) ? named : h.Segment!,
            h.Labels?.GetValueOrDefault(EnclaveSliceMapper.PoolLabel)!,
            h.Roles?.Contains(EnclaveSliceMapper.ClientRole) == true && h.Labels?.GetValueOrDefault(EnclaveSliceMapper.ConsoleLabel) != "true")).ToList();
    }

    /// <summary>
    /// GHOSTS's terrain status, from the job GHOSTS asked for first: GraySpace runs one job at a time and changes an
    /// instance's state only when a job starts, so a teardown or a retry waiting its turn leaves the instance as it was.
    /// </summary>
    private static string StatusOf(JobRecord job, InstanceView instance)
    {
        if (job.Kind == "destroy_instance")
            return job.State switch
            {
                JobState.Succeeded => TerrainStatus.Deleted,
                JobState.Failed or JobState.Cancelled => TerrainStatus.Failed,
                _ => TerrainStatus.TearingDown
            };
        return job.State switch
        {
            JobState.Queued => TerrainStatus.Queued,
            JobState.Failed or JobState.Cancelled => TerrainStatus.Failed,
            _ when instance.State == InstanceState.Ready => TerrainStatus.Ready,
            JobState.Succeeded => instance.State == InstanceState.Destroyed ? TerrainStatus.Deleted : TerrainStatus.Failed,
            _ => TerrainStatus.Building
        };
    }

    /// <summary>A build GraySpace's findings refuse: the terrain is failed, with the findings, and keeps a failed build's job to retry.</summary>
    private async Task<ExecutionTerrainDto> RefuseAsync(Execution execution, ExecutionTerrainDto terrain, ExecutionTerrainDto current, List<Finding> errors, CancellationToken ct)
    {
        var keep = current.Status == TerrainStatus.Failed;
        terrain = terrain with
        {
            InstanceId = keep ? current.InstanceId : null,
            JobId = keep ? current.JobId : null,
            Version = keep && current.JobId != null ? current.Version : terrain.Version,
            Status = TerrainStatus.Failed,
            Error = $"GraySpace found {errors.Count} error(s) in the enclave: " + string.Join("; ", errors.Select(f => $"{f.Code} at {f.Path}: {f.Message}"))
        };
        Write(execution, terrain);
        AddEvent(execution, "TerrainRefused", terrain.Error);
        await context.SaveChangesAsync(ct);
        return Read(execution);
    }

    /// <summary>How far the job is through its stages, 0–100.</summary>
    private static int Progress(JobRecord job)
    {
        if (job.State == JobState.Succeeded) return 100;
        if (job.State == JobState.Queued || job.Stages is not { Count: > 0 } || job.Stage == null) return 0;
        var index = job.Stages.IndexOf(job.Stage);
        return index < 0 ? 0 : (int)(100.0 * index / job.Stages.Count);
    }

    private async Task<Execution> ExecutionAsync(int executionId, CancellationToken ct) =>
        await context.Set<Execution>().FirstOrDefaultAsync(e => e.Id == executionId, ct)
        ?? throw new InvalidOperationException("Execution not found");

    /// <summary>An execution's terrain from its column, or an empty one.</summary>
    public static ExecutionTerrainDto Read(Execution execution) =>
        (string.IsNullOrEmpty(execution.Terrain) ? null : JsonSerializer.Deserialize<ExecutionTerrainDto>(execution.Terrain, Stored))
        ?? new ExecutionTerrainDto(execution.Id, null, null, null, null, execution.TerrainStatus, 0, [], null, null, null);

    private static void Write(Execution execution, ExecutionTerrainDto terrain)
    {
        execution.Terrain = JsonSerializer.Serialize(terrain, Stored);
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
            Severity = type is "TerrainFailed" or "TerrainRefused" ? "Warning" : "Info"
        });

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
