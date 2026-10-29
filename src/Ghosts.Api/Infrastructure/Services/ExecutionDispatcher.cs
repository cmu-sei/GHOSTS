// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NCrontab;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

/// <summary>The exercise clock: wall time since start, less the time spent paused. It stands still while paused and once the run has ended.</summary>
public static class ExecutionClock
{
    public static double ElapsedMinutes(Execution execution, DateTime nowUtc)
    {
        if (execution.StartedAt == null) return 0;
        var end = execution.PausedAt ?? execution.CompletedAt ?? nowUtc;
        return Math.Max(0, ((end - execution.StartedAt.Value).TotalSeconds - execution.PausedSeconds) / 60.0);
    }
}

public interface IExecutionDispatcher
{
    /// <summary>Fires every running execution's items that are due at <paramref name="nowUtc"/>. Returns how many were sent to clients.</summary>
    Task<int> DispatchDueAsync(DateTime nowUtc, CancellationToken ct);
}

/// <summary>
/// Decides, on the server, when each scenario event happens. An NPC item that comes due goes to the clients of the
/// NPCs bound to the run as a one-shot handler; a White Cell item becomes Ready for a person. Nothing is sent at
/// start, so pausing stops events and resuming replays nothing. Workflow items stay with ExecutionWorkflowScheduler.
/// </summary>
public class ExecutionDispatcher(DbContext context, IClientHubService clientHub, IHubContext<ExecutionHub> executionHub) : IExecutionDispatcher
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly string[] Terminal = ["Completed", "Failed", "Skipped"];

    private DbSet<Execution> Executions => context.Set<Execution>();
    private DbSet<ExecutionTimelineItem> Items => context.Set<ExecutionTimelineItem>();

    public async Task<int> DispatchDueAsync(DateTime nowUtc, CancellationToken ct)
    {
        var running = await Executions
            .Where(e => e.Status == ExecutionStatus.Running && e.StartedAt != null)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var execution in running)
        {
            // One run that cannot be dispatched (a malformed item, a failed save) must not stop the others.
            try
            {
                sent += await DispatchExecutionAsync(execution, nowUtc, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.Error(ex, $"[Execution {execution.Id}] Dispatch failed; its unsaved changes are discarded and it is retried next poll");
                DiscardChanges();
            }
        }

        return sent;
    }

    private async Task<int> DispatchExecutionAsync(Execution execution, DateTime nowUtc, CancellationToken ct)
    {
        var sent = 0;
        if (await EndIfDurationPassedAsync(execution, nowUtc, ct)) return 0;

        var elapsed = ExecutionClock.ElapsedMinutes(execution, nowUtc);
        var state = RunState.Read(execution);
        var metBefore = state.ObjectivesMet.Count;
        await ExecutionStateService.ObjectivesAsync(context, execution, state, elapsed, ct);
        if (state.ObjectivesMet.Count != metBefore)
        {
            // Saved now, onto the state as it is now, so the sends below do not hold a stale copy to write back.
            var fresh = await RunState.ReadFreshAsync(context, execution, ct);
            foreach (var (id, at) in state.ObjectivesMet) fresh.ObjectivesMet.TryAdd(id, at);
            RunState.Write(execution, fresh);
            await context.SaveChangesAsync(ct);
        }
        var items = await Items
            .Where(i => i.ExecutionId == execution.Id
                        && (i.AutomationKind == "MachineUpdate" || i.AutomationKind == "Manual"))
            .OrderBy(i => i.Number)
            .ToListAsync(ct);

        List<Seat> seats = null;
        foreach (var item in items.Where(i => IsDue(i, execution, state, elapsed, nowUtc)))
        {
            if (item.AutomationKind == "Manual")
            {
                item.Status = "Ready";
                item.LastFiredAt = nowUtc;
                await BroadcastAsync(execution.Id, "TimelineItemUpdate", new { itemId = item.Id, status = item.Status });
                continue;
            }

            seats ??= await SeatsAsync(execution, ct);
            var extras = ScenarioExtras.Read<EventExtrasDto>(item.Extras);
            var machines = Targeted(seats, extras?.Execution?.Targets).Select(s => s.MachineId).Distinct().ToList();
            if (machines.Count == 0)
            {
                item.Status = "Ready";
                item.LastFiredAt = nowUtc;
                context.Set<ExecutionEvent>().Add(new ExecutionEvent
                {
                    ExecutionId = execution.Id,
                    Timestamp = nowUtc,
                    EventType = "NoBoundNpcs",
                    Description = $"Timeline item #{item.Number} came due with no targeted NPC on a registered machine; it is waiting for White Cell",
                    Data = JsonSerializer.Serialize(new { itemId = item.Id }),
                    Severity = "Warning"
                });
                await BroadcastAsync(execution.Id, "TimelineItemUpdate", new { itemId = item.Id, status = item.Status });
                continue;
            }

            foreach (var machineId in machines)
            {
                var update = OneShot(machineId, execution.Id, item, extras?.Execution?.Action, nowUtc);
                if (!await clientHub.SendUpdate(machineId, update))
                    context.Set<MachineUpdate>().Add(update);
            }

            item.Status = "Deployed";
            item.LastFiredAt = nowUtc;
            item.FireCount++;
            sent++;
            _log.Info($"[Execution {execution.Id}] Dispatched item #{item.Number} to {machines.Count} machine(s) at T+{elapsed:F1}m");
            await BroadcastAsync(execution.Id, "TimelineItemUpdate", new { itemId = item.Id, status = item.Status });
        }

        await context.SaveChangesAsync(ct);
        return sent;
    }

    /// <summary>
    /// Removes the one-shots still queued for clients without a socket, so a run that is paused, stopped or over
    /// sends nothing more, and a later run on the same machines does not carry them out.
    /// </summary>
    public static async Task WithdrawQueuedAsync(DbContext context, int executionId, CancellationToken ct)
    {
        var queued = await context.Set<MachineUpdate>().Where(u => u.ExecutionId == executionId && u.Status == StatusType.Active).ToListAsync(ct);
        context.Set<MachineUpdate>().RemoveRange(queued);
    }

    private void DiscardChanges()
    {
        foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
        {
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
                continue;
            }
            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
        }
    }

    /// <summary>
    /// A point-in-time item comes due once its offset passes; a triggered one once its offset passes and its condition
    /// holds against the run's state; a scheduled one at each cron occurrence after its offset.
    /// </summary>
    private static bool IsDue(ExecutionTimelineItem item, Execution execution, ExecutionRunState state, double elapsedMinutes, DateTime nowUtc)
    {
        var offset = ExecutionService.ParseTimeOffsetMinutes(item.Time);
        switch (item.TriggerKind)
        {
            case TriggerKind.PointInTime:
                return item.Status == "Pending" && elapsedMinutes >= offset;
            case TriggerKind.Scheduled:
                if (Terminal.Contains(item.Status) || string.IsNullOrWhiteSpace(item.Schedule)) return false;
                if (item.AutomationKind == "Manual" && item.Status == "Ready") return false;
                if (elapsedMinutes < offset) return false;
                var next = NextOccurrence(item.Schedule, item.LastFiredAt ?? execution.StartedAt!.Value.AddMinutes(offset));
                return next != null && nowUtc >= next.Value;
            case TriggerKind.Triggered:
                return item.Status == "Pending" && elapsedMinutes >= offset
                       && ConditionEvaluator.Holds(item.TriggerCondition, state, elapsedMinutes);
            default:
                return false;
        }
    }

    private static DateTime? NextOccurrence(string cron, DateTime after)
    {
        try
        {
            var parts = cron.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var schedule = CrontabSchedule.Parse(cron, new CrontabSchedule.ParseOptions { IncludingSeconds = parts.Length == 6 });
            return schedule.GetNextOccurrence(after);
        }
        catch (Exception ex)
        {
            _log.Warn($"Invalid cron schedule '{cron}': {ex.Message}");
            return null;
        }
    }

    /// <summary>An NPC on a registered machine: its pool role, the host it sits at, and that host's segment.</summary>
    private record Seat(Guid MachineId, string Team, string HostName, string Segment);

    /// <summary>
    /// The NPCs this run can reach: its pool NPCs on a machine, by MachineId or by a host name that a registered
    /// client matches exactly (cached onto the NPC), and the latest compilation's assignments.
    /// </summary>
    private async Task<List<Seat>> SeatsAsync(Execution execution, CancellationToken ct)
    {
        var segments = await SegmentsByHostAsync(execution, ct);
        var seats = new List<Seat>();

        var npcs = await context.Set<NpcRecord>()
            .Where(n => n.ExecutionId == execution.Id && (n.MachineId != null || n.HostName != null))
            .ToListAsync(ct);
        foreach (var npc in npcs)
        {
            if (npc.MachineId == null)
            {
                var machine = await MachineService.FindMatchAsync(context.Set<Machine>(),
                    new Machine { Name = npc.HostName, FQDN = string.Empty, Host = string.Empty, ResolvedHost = string.Empty }, "name", ct);
                if (machine == null) continue;
                npc.MachineId = machine.Id;
            }
            var host = npc.HostName ?? await MachineNameAsync(npc.MachineId.Value, ct);
            seats.Add(new Seat(npc.MachineId.Value, npc.Team, host, host != null && segments.TryGetValue(host, out var seg) ? seg : null));
        }

        var compilationId = await context.Set<ScenarioCompilation>()
            .Where(c => c.ScenarioId == execution.ScenarioId && c.Status == "Completed")
            .OrderByDescending(c => c.CompletedAt)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync(ct);
        if (compilationId != null)
        {
            var compiled = await context.Set<ScenarioNpcAssignment>().Where(a => a.CompilationId == compilationId).Select(a => a.MachineId).ToListAsync(ct);
            foreach (var machineId in compiled)
            {
                var host = await MachineNameAsync(machineId, ct);
                seats.Add(new Seat(machineId, null, host, host != null && segments.TryGetValue(host, out var seg) ? seg : null));
            }
        }

        return seats;
    }

    /// <summary>The seats an item's targets name, by pool role, segment or host; every seat when there are no targets.</summary>
    private static IEnumerable<Seat> Targeted(List<Seat> seats, TargetsDto targets)
    {
        if (targets == null || (targets.Pools is not { Count: > 0 } && targets.Segments is not { Count: > 0 } && targets.Hosts is not { Count: > 0 }))
            return seats;
        bool In(List<string> names, string value) => value != null && names is { Count: > 0 } && names.Contains(value, StringComparer.OrdinalIgnoreCase);
        return seats.Where(s => In(targets.Pools, s.Team) || In(targets.Segments, s.Segment) || In(targets.Hosts, s.HostName));
    }

    private async Task<Dictionary<string, string>> SegmentsByHostAsync(Execution execution, CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (execution.TerrainStatus == Terrain.TerrainStatus.Ready && !string.IsNullOrEmpty(execution.Terrain))
        {
            var manifest = JsonSerializer.Deserialize<Terrain.ExecutionTerrainDto>(execution.Terrain, Terrain.GraySpaceTerrainProvider.Json)?.Manifest;
            foreach (var h in manifest?.Hosts ?? []) map.TryAdd(h.Name, h.Segment);
        }
        var extras = ScenarioExtras.Read<ScenarioExtrasDto>(
            await context.Set<Scenario>().Where(s => s.Id == execution.ScenarioId).Select(s => s.Extras).FirstOrDefaultAsync(ct));
        foreach (var h in extras?.Terrain?.Hosts ?? [])
            if (!string.IsNullOrEmpty(h.Name)) map.TryAdd(h.Name, h.Segment);
        return map;
    }

    private Task<string> MachineNameAsync(Guid machineId, CancellationToken ct) =>
        context.Set<Machine>().Where(m => m.Id == machineId).Select(m => m.Name).FirstOrDefaultAsync(ct);

    /// <summary>
    /// What the client runs now, with no delay and no loop: the item's NPC action as its own handler when it has one,
    /// then the report that completes the item, naming it by id.
    /// </summary>
    private static MachineUpdate OneShot(Guid machineId, int executionId, ExecutionTimelineItem item, NpcActionDto action, DateTime nowUtc)
    {
        var handlers = new List<TimelineHandler>();
        if (action != null && Enum.TryParse<HandlerType>(action.Handler, ignoreCase: false, out var handlerType))
        {
            var act = new TimelineHandler { HandlerType = handlerType, Initial = action.Initial ?? string.Empty };
            foreach (var (k, v) in action.Args ?? new Dictionary<string, string>()) act.HandlerArgs[k] = v;
            var commands = action.Commands is { Count: > 0 } ? action.Commands : [DefaultCommand(handlerType)];
            foreach (var command in commands)
                act.TimeLineEvents.Add(new TimelineEvent { Command = command, CommandArgs = (action.CommandArgs ?? []).Cast<object>().ToList(), DelayBefore = 0, DelayAfter = 1000 });
            handlers.Add(act);
        }

        var report = new TimelineHandler
        {
            HandlerType = HandlerType.NpcSystem,
            Initial = $"scenario-execution:{executionId}:item:{item.Id}"
        };
        report.TimeLineEvents.Add(new TimelineEvent
        {
            Command = "npc-scenario-action",
            CommandArgs = new List<object>
            {
                $"execution:{executionId}",
                $"item:{item.Id}",
                $"event:{item.Number}",
                item.Description ?? string.Empty
            },
            DelayBefore = 0,
            DelayAfter = 0
        });
        handlers.Add(report);

        return new MachineUpdate
        {
            MachineId = machineId,
            ExecutionId = executionId,
            Status = StatusType.Active,
            Type = UpdateClientConfig.UpdateType.TimelinePartial,
            ActiveUtc = nowUtc,
            CreatedUtc = nowUtc,
            Update = new Timeline { Id = Guid.NewGuid(), Status = Timeline.TimelineStatus.Run, TimeLineHandlers = handlers }
        };
    }

    /// <summary>The command a handler runs when an action names none, as the clients' own handlers expect.</summary>
    private static string DefaultCommand(HandlerType handler) => handler switch
    {
        HandlerType.BrowserChrome or HandlerType.BrowserFirefox => "random",
        HandlerType.Print => "print",
        HandlerType.Outlook => "read",
        _ => "create"
    };

    /// <summary>Completes the run once its duration has passed: items that never fired are skipped, recurring ones complete.</summary>
    private async Task<bool> EndIfDurationPassedAsync(Execution execution, DateTime nowUtc, CancellationToken ct)
    {
        var hours = await context.Set<GameMechanics>()
            .Where(g => g.ScenarioId == execution.ScenarioId)
            .Select(g => g.DurationHours)
            .FirstOrDefaultAsync(ct);
        if (hours <= 0)
        {
            hours = await context.Set<ScenarioTimeline>()
                .Where(t => t.ScenarioId == execution.ScenarioId)
                .Select(t => t.ExerciseDuration)
                .FirstOrDefaultAsync(ct);
        }
        if (hours <= 0 || ExecutionClock.ElapsedMinutes(execution, nowUtc) < hours * 60) return false;

        var open = await Items
            .Where(i => i.ExecutionId == execution.Id && !Terminal.Contains(i.Status))
            .ToListAsync(ct);
        foreach (var item in open)
        {
            item.Status = item.FireCount > 0 ? "Completed" : "Skipped";
            item.CompletedAt = nowUtc;
            item.CompletedBy = "duration-elapsed";
        }

        execution.Status = ExecutionStatus.Completed;
        execution.CompletedAt = nowUtc;
        await WithdrawQueuedAsync(context, execution.Id, ct);
        context.Set<ExecutionEvent>().Add(new ExecutionEvent
        {
            ExecutionId = execution.Id,
            Timestamp = nowUtc,
            EventType = "StatusChange",
            Description = $"Execution completed: its {hours}h duration elapsed",
            Data = JsonSerializer.Serialize(new { status = "Completed", skipped = open.Count(i => i.Status == "Skipped") }),
            Severity = "Info"
        });
        await context.SaveChangesAsync(ct);
        _log.Info($"[Execution {execution.Id}] Completed at its {hours}h duration; {open.Count} open item(s) closed");
        await BroadcastAsync(execution.Id, "StatusChange", new { status = "Completed" });
        return true;
    }

    private async Task BroadcastAsync(int executionId, string updateType, object data)
    {
        try
        {
            var connections = ExecutionHub.GetConnections();
            var message = new { executionId, updateType, data, timestamp = DateTime.UtcNow };
            foreach (var id in connections.GetConnections(executionId.ToString()).Concat(connections.GetConnections("all")))
                await executionHub.Clients.Client(id).SendAsync("ExecutionUpdate", message);
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to broadcast execution update: {ex.Message}");
        }
    }
}
