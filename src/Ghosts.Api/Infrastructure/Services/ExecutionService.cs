// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Microsoft.EntityFrameworkCore;
using NLog;
using System.Text.Json;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IExecutionService
    {
        Task<List<ExecutionSummaryDto>> GetAllAsync(int? scenarioId, CancellationToken ct);
        Task<Execution> GetByIdAsync(int id, CancellationToken ct);
        Task<Execution> CreateAsync(CreateExecutionDto dto, CancellationToken ct);
        Task<Execution> UpdateAsync(int id, UpdateExecutionDto dto, CancellationToken ct);
        Task DeleteAsync(int id, CancellationToken ct);
        Task<Execution> StartAsync(int id, CancellationToken ct);
        Task<Execution> PauseAsync(int id, CancellationToken ct);
        Task<Execution> StopAsync(int id, bool failed, CancellationToken ct);
        Task<Execution> CancelAsync(int id, CancellationToken ct);
        Task<List<ExecutionEvent>> GetEventsAsync(int id, string eventType, int? limit, CancellationToken ct);
        Task<ExecutionEvent> AddEventAsync(int id, CreateExecutionEventDto dto, CancellationToken ct);
        Task<List<ExecutionMetricSnapshot>> GetMetricsAsync(int id, int? limit, CancellationToken ct);
        Task<ExecutionMetricSnapshot> AddMetricSnapshotAsync(int id, JsonElement metrics, CancellationToken ct);
        Task UpdateMetricsAsync(int id, JsonElement metrics, CancellationToken ct);
        Task SetErrorAsync(int id, JsonElement error, CancellationToken ct);
    }

    /// <summary>
    /// Accepts DbContext (base) so that both production (ApplicationDbContext) and
    /// test (TestDbContext) instances can be injected without relational-provider friction.
    /// </summary>
    public class ExecutionService(DbContext context, IClientHubService clientHubService) : IExecutionService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DbContext _context = context;
        private readonly IClientHubService _clientHubService = clientHubService;

        // ── Convenience accessors ──────────────────────────────────────────────
        private DbSet<Execution> Executions => _context.Set<Execution>();
        private DbSet<Scenario> Scenarios => _context.Set<Scenario>();
        private DbSet<ExecutionEvent> ExecutionEvents => _context.Set<ExecutionEvent>();
        private DbSet<ExecutionMetricSnapshot> ExecutionMetricSnapshots => _context.Set<ExecutionMetricSnapshot>();
        private DbSet<ScenarioCompilation> ScenarioCompilations => _context.Set<ScenarioCompilation>();
        private DbSet<ScenarioNpcAssignment> ScenarioNpcAssignments => _context.Set<ScenarioNpcAssignment>();
        private DbSet<ScenarioTimelineEvent> ScenarioTimelineEvents => _context.Set<ScenarioTimelineEvent>();
        private DbSet<MachineUpdate> MachineUpdates => _context.Set<MachineUpdate>();

        public async Task<List<ExecutionSummaryDto>> GetAllAsync(int? scenarioId, CancellationToken ct)
        {
            var query = Executions
                .Include(e => e.Scenario)
                .Include(e => e.Events)
                .Include(e => e.MetricSnapshots)
                .AsQueryable();

            if (scenarioId.HasValue)
            {
                query = query.Where(e => e.ScenarioId == scenarioId.Value);
            }

            var executions = await query.OrderByDescending(e => e.CreatedAt).ToListAsync(ct);

            return executions.Select(execution =>
            {
                int? durationSeconds = null;
                if (execution.StartedAt.HasValue)
                {
                    var endTime = execution.CompletedAt ?? DateTime.UtcNow;
                    durationSeconds = (int)(endTime - execution.StartedAt.Value).TotalSeconds;
                }

                return new ExecutionSummaryDto(
                    execution.Id,
                    execution.Name,
                    execution.Status.ToString(),
                    execution.CreatedAt,
                    execution.StartedAt,
                    execution.CompletedAt,
                    durationSeconds,
                    execution.Scenario?.Name ?? "Unknown",
                    execution.Events.Count,
                    execution.MetricSnapshots.Count
                );
            }).ToList();
        }

        public async Task<Execution> GetByIdAsync(int id, CancellationToken ct)
        {
            var execution = await Executions
                .Include(e => e.Scenario)
                .FirstOrDefaultAsync(e => e.Id == id, ct);

            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            return execution;
        }

        public async Task<Execution> CreateAsync(CreateExecutionDto dto, CancellationToken ct)
        {
            var scenario = await Scenarios.FindAsync(dto.ScenarioId);
            if (scenario == null)
            {
                _log.Error($"Scenario not found: {dto.ScenarioId}");
                throw new InvalidOperationException($"Scenario with id {dto.ScenarioId} not found");
            }

            var executionCount = await Executions.CountAsync(e => e.ScenarioId == dto.ScenarioId, ct);
            var executionName = string.IsNullOrEmpty(dto.Name)
                ? $"{scenario.Name} - Run {executionCount + 1}"
                : dto.Name;

            var execution = new Execution
            {
                ScenarioId = dto.ScenarioId,
                Name = executionName,
                Description = dto.Description ?? string.Empty,
                Status = ExecutionStatus.Created,
                CreatedAt = DateTime.UtcNow,
                ParameterOverrides = dto.ParameterOverrides ?? "{}",
                Configuration = dto.Configuration ?? "{}",
                Metrics = "{}",
                ErrorDetails = "{}"
            };

            Executions.Add(execution);

            var initialEvent = new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "Created",
                Description = $"Execution '{execution.Name}' created",
                Data = "{}",
                Severity = "Info"
            };
            execution.Events.Add(initialEvent);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create execution: {operation}");
                throw new InvalidOperationException("Could not create Execution");
            }

            _log.Info($"Created execution: {execution.Id} - {execution.Name} for scenario {dto.ScenarioId}");

            return await GetByIdAsync(execution.Id, ct);
        }

        public async Task<Execution> UpdateAsync(int id, UpdateExecutionDto dto, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            if (!string.IsNullOrEmpty(dto.Name)) execution.Name = dto.Name;
            if (dto.Description != null) execution.Description = dto.Description;
            if (dto.ParameterOverrides != null) execution.ParameterOverrides = dto.ParameterOverrides;
            if (dto.Configuration != null) execution.Configuration = dto.Configuration;

            await _context.SaveChangesAsync(ct);
            _log.Info($"Updated execution: {id}");
            return await GetByIdAsync(id, ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            if (execution.Status == ExecutionStatus.Running || execution.Status == ExecutionStatus.Paused)
            {
                _log.Error($"Cannot delete running or paused execution: {id}");
                throw new InvalidOperationException("Cannot delete a running or paused execution. Stop it first.");
            }

            Executions.Remove(execution);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not delete execution: {operation}");
                throw new InvalidOperationException("Could not delete Execution");
            }

            _log.Info($"Deleted execution: {id}");
        }

        public async Task<Execution> StartAsync(int id, CancellationToken ct)
        {
            var execution = await GetByIdAsync(id, ct);

            if (execution.Status != ExecutionStatus.Created && execution.Status != ExecutionStatus.Paused)
            {
                _log.Error($"Cannot start execution with status {execution.Status}: {id}");
                throw new InvalidOperationException($"Cannot start execution with status {execution.Status}");
            }

            // ── Guard 1: a completed compilation must exist ──────────────────
            var compilation = await ScenarioCompilations
                .Where(c => c.ScenarioId == execution.ScenarioId && c.Status == "Completed")
                .OrderByDescending(c => c.CompletedAt)
                .FirstOrDefaultAsync(ct);

            if (compilation == null)
            {
                throw new InvalidOperationException(
                    "Cannot start execution: no completed compilation exists for this scenario. " +
                    "Use the Scenario Builder to compile the scenario first.");
            }

            // ── Guard 2: NPC-to-machine assignments must exist ───────────────
            var assignments = await ScenarioNpcAssignments
                .Where(a => a.CompilationId == compilation.Id)
                .ToListAsync(ct);

            if (assignments.Count == 0)
            {
                throw new InvalidOperationException(
                    "Cannot start execution: no NPC-to-machine assignments found for the selected compilation. " +
                    "Assign machines to compiled NPCs in the Scenario Builder before starting.");
            }

            // ── Load scenario timeline events ────────────────────────────────
            var timelineEvents = await ScenarioTimelineEvents
                .Where(e => e.Timeline.ScenarioId == execution.ScenarioId)
                .OrderBy(e => e.Number)
                .ToListAsync(ct);

            // ── Deploy: create MachineUpdate per assignment ──────────────────
            var deployedCount = 0;
            var deployErrors = new List<string>();

            foreach (var assignment in assignments)
            {
                try
                {
                    var timeline = BuildTimelineForNpc(assignment.NpcId, execution.Id, timelineEvents);

                    var machineUpdate = new MachineUpdate
                    {
                        MachineId = assignment.MachineId,
                        Status = StatusType.Active,
                        Update = timeline,
                        ActiveUtc = DateTime.UtcNow,
                        CreatedUtc = DateTime.UtcNow,
                        Type = UpdateClientConfig.UpdateType.TimelinePartial
                    };

                    // Attempt real-time delivery; fall back to DB polling path
                    var delivered = await _clientHubService.SendUpdate(assignment.MachineId, machineUpdate);
                    if (!delivered)
                    {
                        MachineUpdates.Add(machineUpdate);
                        _log.Debug($"[Execution {id}] NPC {assignment.NpcId} → machine {assignment.MachineId}: queued in DB (no active socket)");
                    }
                    else
                    {
                        _log.Debug($"[Execution {id}] NPC {assignment.NpcId} → machine {assignment.MachineId}: delivered via websocket");
                    }

                    deployedCount++;
                }
                catch (Exception ex)
                {
                    var msg = $"Failed to deploy NPC {assignment.NpcId} to machine {assignment.MachineId}: {ex.Message}";
                    _log.Warn(msg);
                    deployErrors.Add(msg);
                }
            }

            // Require at least one successful deployment
            if (deployedCount == 0)
            {
                var errorSummary = string.Join("; ", deployErrors);
                throw new InvalidOperationException(
                    $"Execution start failed: all {assignments.Count} deployment(s) failed. Errors: {errorSummary}");
            }

            // ── Transition state ─────────────────────────────────────────────
            execution.Status = ExecutionStatus.Running;
            if (execution.StartedAt == null)
            {
                execution.StartedAt = DateTime.UtcNow;
            }

            var startEvent = new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution started",
                Data = JsonSerializer.Serialize(new
                {
                    status = "Running",
                    timestamp = DateTime.UtcNow,
                    compilationId = compilation.Id,
                    deploymentsAttempted = assignments.Count,
                    deploymentsSucceeded = deployedCount,
                    deployErrors
                }),
                Severity = deployErrors.Count > 0 ? "Warning" : "Info"
            };
            ExecutionEvents.Add(startEvent);

            await _context.SaveChangesAsync(ct);

            _log.Info($"Started execution {id}: deployed to {deployedCount}/{assignments.Count} machines" +
                      (deployErrors.Count > 0 ? $" ({deployErrors.Count} errors)" : ""));

            return execution;
        }

        /// <summary>
        /// Builds a GHOSTS-native Timeline for an NPC from the scenario's timeline events.
        /// Uses NpcSystem handler so the client knows it is operating in NPC-driven mode.
        /// </summary>
        private static Timeline BuildTimelineForNpc(
            Guid npcId,
            int executionId,
            List<ScenarioTimelineEvent> scenarioEvents)
        {
            var ghostsEvents = new List<TimelineEvent>();

            foreach (var evt in scenarioEvents)
            {
                ghostsEvents.Add(new TimelineEvent
                {
                    Command = "npc-scenario-action",
                    CommandArgs = new List<object>
                    {
                        $"execution:{executionId}",
                        $"event:{evt.Number}",
                        evt.Description ?? string.Empty
                    },
                    DelayBefore = ParseTimeOffsetMs(evt.Time),
                    DelayAfter = 0
                });
            }

            // Ensure at least one event
            if (ghostsEvents.Count == 0)
            {
                ghostsEvents.Add(new TimelineEvent
                {
                    Command = "npc-scenario-action",
                    CommandArgs = new List<object> { $"execution:{executionId}", "event:1", "default" },
                    DelayBefore = 0,
                    DelayAfter = 60000
                });
            }

            var handler = new TimelineHandler
            {
                HandlerType = HandlerType.NpcSystem,
                Initial = $"scenario-execution:{executionId}:npc:{npcId}"
            };
            handler.TimeLineEvents.AddRange(ghostsEvents);

            return new Timeline
            {
                Id = Guid.NewGuid(),
                Status = Timeline.TimelineStatus.Run,
                TimeLineHandlers = new List<TimelineHandler> { handler }
            };
        }

        /// <summary>Parses "T+15m" → 900000 ms. Returns 0 for any unparseable input.</summary>
        public static int ParseTimeOffsetMs(string? time)
        {
            if (string.IsNullOrWhiteSpace(time)) return 0;
            var m = Regex.Match(time, @"T\+(\d+)m", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            return int.Parse(m.Groups[1].Value) * 60_000;
        }

        public async Task<Execution> PauseAsync(int id, CancellationToken ct)
        {
            var execution = await GetByIdAsync(id, ct);

            if (execution.Status != ExecutionStatus.Running)
            {
                _log.Error($"Can only pause running executions: {id}");
                throw new InvalidOperationException("Can only pause running executions");
            }

            execution.Status = ExecutionStatus.Paused;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution paused",
                Data = JsonSerializer.Serialize(new { status = "Paused", timestamp = DateTime.UtcNow }),
                Severity = "Info"
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"Paused execution: {id}");
            return execution;
        }

        public async Task<Execution> StopAsync(int id, bool failed, CancellationToken ct)
        {
            var execution = await GetByIdAsync(id, ct);

            if (execution.Status == ExecutionStatus.Completed ||
                execution.Status == ExecutionStatus.Failed ||
                execution.Status == ExecutionStatus.Cancelled)
            {
                _log.Error($"Execution already in terminal state {execution.Status}: {id}");
                throw new InvalidOperationException($"Execution already in terminal state: {execution.Status}");
            }

            execution.Status = failed ? ExecutionStatus.Failed : ExecutionStatus.Completed;
            execution.CompletedAt = DateTime.UtcNow;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = failed ? "Execution failed" : "Execution completed",
                Data = JsonSerializer.Serialize(new { status = execution.Status.ToString(), timestamp = DateTime.UtcNow }),
                Severity = failed ? "Error" : "Info"
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"Stopped execution: {id} with status {execution.Status}");
            return execution;
        }

        public async Task<Execution> CancelAsync(int id, CancellationToken ct)
        {
            var execution = await GetByIdAsync(id, ct);

            if (execution.Status == ExecutionStatus.Completed ||
                execution.Status == ExecutionStatus.Failed ||
                execution.Status == ExecutionStatus.Cancelled)
            {
                _log.Error($"Execution already in terminal state {execution.Status}: {id}");
                throw new InvalidOperationException($"Execution already in terminal state: {execution.Status}");
            }

            execution.Status = ExecutionStatus.Cancelled;
            execution.CompletedAt = DateTime.UtcNow;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution cancelled by user",
                Data = JsonSerializer.Serialize(new { status = "Cancelled", timestamp = DateTime.UtcNow }),
                Severity = "Warning"
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"Cancelled execution: {id}");
            return execution;
        }

        public async Task<List<ExecutionEvent>> GetEventsAsync(int id, string eventType, int? limit, CancellationToken ct)
        {
            var query = ExecutionEvents
                .Where(e => e.ExecutionId == id)
                .OrderByDescending(e => e.Timestamp)
                .AsQueryable();

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType == eventType);

            if (limit.HasValue && limit.Value > 0)
                query = query.Take(limit.Value);

            return await query.ToListAsync(ct);
        }

        public async Task<ExecutionEvent> AddEventAsync(int id, CreateExecutionEventDto dto, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            var executionEvent = new ExecutionEvent
            {
                ExecutionId = id,
                Timestamp = DateTime.UtcNow,
                EventType = dto.EventType,
                Description = dto.Description,
                Data = dto.Data ?? "{}",
                Severity = dto.Severity ?? "Info"
            };

            ExecutionEvents.Add(executionEvent);
            await _context.SaveChangesAsync(ct);
            return executionEvent;
        }

        public async Task<List<ExecutionMetricSnapshot>> GetMetricsAsync(int id, int? limit, CancellationToken ct)
        {
            var query = ExecutionMetricSnapshots
                .Where(ms => ms.ExecutionId == id)
                .OrderByDescending(ms => ms.Timestamp)
                .AsQueryable();

            if (limit.HasValue && limit.Value > 0)
                query = query.Take(limit.Value);

            return await query.ToListAsync(ct);
        }

        public async Task<ExecutionMetricSnapshot> AddMetricSnapshotAsync(int id, JsonElement metrics, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            var elapsedSeconds = execution.StartedAt.HasValue
                ? (int)(DateTime.UtcNow - execution.StartedAt.Value).TotalSeconds
                : 0;

            var snapshot = new ExecutionMetricSnapshot
            {
                ExecutionId = id,
                Timestamp = DateTime.UtcNow,
                ElapsedSeconds = elapsedSeconds,
                Metrics = metrics.GetRawText()
            };

            ExecutionMetricSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(ct);
            return snapshot;
        }

        public async Task UpdateMetricsAsync(int id, JsonElement metrics, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            execution.Metrics = metrics.GetRawText();
            await _context.SaveChangesAsync(ct);
        }

        public async Task SetErrorAsync(int id, JsonElement error, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            execution.ErrorDetails = error.GetRawText();
            execution.Status = ExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = id,
                Timestamp = DateTime.UtcNow,
                EventType = "Error",
                Description = "Execution encountered an error",
                Data = error.GetRawText(),
                Severity = "Error"
            });

            await _context.SaveChangesAsync(ct);
            _log.Error($"Execution {id} failed with error: {error.GetRawText()}");
        }
    }
}
