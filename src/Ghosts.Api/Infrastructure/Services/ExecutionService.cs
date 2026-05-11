// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services.ClientServices;
using Ghosts.Domain;
using Microsoft.AspNetCore.SignalR;
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
        Task<List<ExecutionTimelineItem>> GetTimelineItemsAsync(int executionId, CancellationToken ct);
        Task<ExecutionTimelineItem> CompleteTimelineItemAsync(int executionId, int itemId, CompleteTimelineItemDto dto, CancellationToken ct);
        Task<ExecutionTimelineItem> ReportTimelineItemResultAsync(int executionId, int itemId, JsonElement resultData, CancellationToken ct);
    }

    /// <summary>
    /// Accepts DbContext (base) so that both production (ApplicationDbContext) and
    /// test (TestDbContext) instances can be injected without relational-provider friction.
    /// </summary>
    public class ExecutionService(DbContext context, IClientHubService clientHubService, IHubContext<ExecutionHub> executionHub) : IExecutionService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly DbContext _context = context;
        private readonly IClientHubService _clientHubService = clientHubService;
        private readonly IHubContext<ExecutionHub> _executionHub = executionHub;

        // ── Convenience accessors ──────────────────────────────────────────────
        private DbSet<Execution> Executions => _context.Set<Execution>();
        private DbSet<Scenario> Scenarios => _context.Set<Scenario>();
        private DbSet<ExecutionEvent> ExecutionEvents => _context.Set<ExecutionEvent>();
        private DbSet<ExecutionMetricSnapshot> ExecutionMetricSnapshots => _context.Set<ExecutionMetricSnapshot>();
        private DbSet<ExecutionTimelineItem> ExecutionTimelineItems => _context.Set<ExecutionTimelineItem>();
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

            // ── Snapshot scenario timeline events into execution-scoped items ──
            var scenarioEvents = await ScenarioTimelineEvents
                .Where(e => e.Timeline.ScenarioId == dto.ScenarioId)
                .OrderBy(e => e.Number)
                .ToListAsync(ct);

            var hasAssignments = await ScenarioNpcAssignments
                .AnyAsync(a => a.ScenarioId == dto.ScenarioId, ct);

            foreach (var evt in scenarioEvents)
            {
                var automationKind = evt.ExecutionType == ExecutionType.Workflow
                    ? "Workflow"
                    : hasAssignments ? "MachineUpdate" : "Manual";

                execution.TimelineItems.Add(new ExecutionTimelineItem
                {
                    SourceTimelineEventId = evt.Id,
                    Time = evt.Time,
                    Number = evt.Number,
                    Assigned = evt.Assigned,
                    Description = evt.Description,
                    Status = "Pending",
                    AutomationKind = automationKind,
                    WorkflowId = evt.WorkflowId,
                    TriggerKind = evt.TriggerKind,
                    Schedule = evt.Schedule,
                    TriggerCondition = evt.TriggerCondition,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create execution: {operation}");
                throw new InvalidOperationException("Could not create Execution");
            }

            _log.Info($"Created execution: {execution.Id} - {execution.Name} for scenario {dto.ScenarioId} with {scenarioEvents.Count} timeline items");

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

            // ── Optional deployment: compile + assign if available ──────────
            var compilation = await ScenarioCompilations
                .Where(c => c.ScenarioId == execution.ScenarioId && c.Status == "Completed")
                .OrderByDescending(c => c.CompletedAt)
                .FirstOrDefaultAsync(ct);

            var deployedCount = 0;
            var deployErrors = new List<string>();
            int? compilationId = null;
            var assignmentCount = 0;

            if (compilation != null)
            {
                compilationId = compilation.Id;

                var assignments = await ScenarioNpcAssignments
                    .Where(a => a.CompilationId == compilation.Id)
                    .ToListAsync(ct);

                assignmentCount = assignments.Count;

                if (assignments.Count > 0)
                {
                    var timelineEvents = await ScenarioTimelineEvents
                        .Where(e => e.Timeline.ScenarioId == execution.ScenarioId)
                        .OrderBy(e => e.Number)
                        .ToListAsync(ct);

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
                }
            }

            // ── Transition timeline items ────────────────────────────────────
            var timelineItems = await ExecutionTimelineItems
                .Where(ti => ti.ExecutionId == id)
                .ToListAsync(ct);

            foreach (var item in timelineItems)
            {
                if (item.AutomationKind == "MachineUpdate" && item.Status == "Pending")
                {
                    item.Status = deployedCount > 0 ? "Deployed" : "Queued";
                }
                else if (item.AutomationKind == "Workflow" && item.Status == "Pending")
                {
                    item.Status = "Queued";
                }
                // Manual items stay Pending
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
                    compilationId,
                    deploymentsAttempted = assignmentCount,
                    deploymentsSucceeded = deployedCount,
                    deployErrors,
                    timelineItemsTotal = timelineItems.Count,
                    automatedItems = timelineItems.Count(ti => ti.AutomationKind == "MachineUpdate"),
                    workflowItems = timelineItems.Count(ti => ti.AutomationKind == "Workflow"),
                    manualItems = timelineItems.Count(ti => ti.AutomationKind == "Manual")
                }),
                Severity = deployErrors.Count > 0 ? "Warning" : "Info"
            };
            ExecutionEvents.Add(startEvent);

            await _context.SaveChangesAsync(ct);

            if (assignmentCount > 0)
            {
                _log.Info($"Started execution {id}: deployed to {deployedCount}/{assignmentCount} machines" +
                          (deployErrors.Count > 0 ? $" ({deployErrors.Count} errors)" : ""));
            }
            else
            {
                _log.Info($"Started execution {id}: no NPC assignments to deploy (scenario not compiled or no assignments)");
            }

            await BroadcastExecutionUpdateAsync(id, "StatusChange", new { status = "Running" });

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
            var handlers = new List<TimelineHandler>();

            // Group events by trigger kind to produce appropriate handler configurations
            var pointInTimeEvents = scenarioEvents.Where(e => e.TriggerKind == TriggerKind.PointInTime).ToList();
            var scheduledEvents = scenarioEvents.Where(e => e.TriggerKind == TriggerKind.Scheduled).ToList();
            var triggeredEvents = scenarioEvents.Where(e => e.TriggerKind == TriggerKind.Triggered).ToList();

            // Point-in-time events: one-shot handler with delay-based ordering
            if (pointInTimeEvents.Count > 0)
            {
                var ghostsEvents = pointInTimeEvents.Select(evt => new TimelineEvent
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
                }).ToList();

                var handler = new TimelineHandler
                {
                    HandlerType = HandlerType.NpcSystem,
                    Initial = $"scenario-execution:{executionId}:npc:{npcId}:point-in-time"
                };
                handler.TimeLineEvents.AddRange(ghostsEvents);
                handlers.Add(handler);
            }

            // Scheduled events: looping handler with cron/interval schedule
            foreach (var evt in scheduledEvents)
            {
                var handler = new TimelineHandler
                {
                    HandlerType = HandlerType.NpcSystem,
                    Initial = $"scenario-execution:{executionId}:npc:{npcId}:scheduled:{evt.Number}",
                    Loop = true,
                    ScheduleType = TimelineHandler.TimelineScheduleType.Cron,
                    Schedule = evt.Schedule
                };
                handler.TimeLineEvents.Add(new TimelineEvent
                {
                    Command = "npc-scenario-action",
                    CommandArgs = new List<object>
                    {
                        $"execution:{executionId}",
                        $"event:{evt.Number}",
                        evt.Description ?? string.Empty
                    },
                    DelayBefore = 0,
                    DelayAfter = 0
                });
                handlers.Add(handler);
            }

            // Triggered events: handler with trigger condition in args (client evaluates)
            foreach (var evt in triggeredEvents)
            {
                var handler = new TimelineHandler
                {
                    HandlerType = HandlerType.NpcSystem,
                    Initial = $"scenario-execution:{executionId}:npc:{npcId}:triggered:{evt.Number}"
                };
                handler.HandlerArgs["triggerCondition"] = evt.TriggerCondition ?? string.Empty;
                handler.TimeLineEvents.Add(new TimelineEvent
                {
                    Command = "npc-scenario-action",
                    CommandArgs = new List<object>
                    {
                        $"execution:{executionId}",
                        $"event:{evt.Number}",
                        $"trigger:{evt.TriggerCondition}",
                        evt.Description ?? string.Empty
                    },
                    DelayBefore = ParseTimeOffsetMs(evt.Time),
                    DelayAfter = 0
                });
                handlers.Add(handler);
            }

            // Ensure at least one handler
            if (handlers.Count == 0)
            {
                var handler = new TimelineHandler
                {
                    HandlerType = HandlerType.NpcSystem,
                    Initial = $"scenario-execution:{executionId}:npc:{npcId}"
                };
                handler.TimeLineEvents.Add(new TimelineEvent
                {
                    Command = "npc-scenario-action",
                    CommandArgs = new List<object> { $"execution:{executionId}", "event:1", "default" },
                    DelayBefore = 0,
                    DelayAfter = 60000
                });
                handlers.Add(handler);
            }

            return new Timeline
            {
                Id = Guid.NewGuid(),
                Status = Timeline.TimelineStatus.Run,
                TimeLineHandlers = handlers
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
            await BroadcastExecutionUpdateAsync(id, "StatusChange", new { status = "Paused" });
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
            await BroadcastExecutionUpdateAsync(id, "StatusChange", new { status = execution.Status.ToString() });
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
            await BroadcastExecutionUpdateAsync(id, "StatusChange", new { status = "Cancelled" });
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

        public async Task<List<ExecutionTimelineItem>> GetTimelineItemsAsync(int executionId, CancellationToken ct)
        {
            return await ExecutionTimelineItems
                .Where(ti => ti.ExecutionId == executionId)
                .OrderBy(ti => ti.Number)
                .ToListAsync(ct);
        }

        public async Task<ExecutionTimelineItem> CompleteTimelineItemAsync(int executionId, int itemId, CompleteTimelineItemDto dto, CancellationToken ct)
        {
            var item = await ExecutionTimelineItems
                .FirstOrDefaultAsync(ti => ti.Id == itemId && ti.ExecutionId == executionId, ct);
            if (item == null)
                throw new InvalidOperationException("Timeline item not found");

            var terminalStatuses = new[] { "Completed", "Failed", "Skipped" };
            if (terminalStatuses.Contains(item.Status))
                throw new InvalidOperationException($"Timeline item already in terminal state: {item.Status}");

            var validStatuses = new[] { "Completed", "Failed", "Skipped" };
            if (!validStatuses.Contains(dto.Status))
                throw new InvalidOperationException($"Invalid status: {dto.Status}. Must be one of: {string.Join(", ", validStatuses)}");

            item.Status = dto.Status;
            item.Notes = dto.Notes;
            item.CompletedBy = dto.CompletedBy ?? "exercise-admin";
            item.CompletedAt = DateTime.UtcNow;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = executionId,
                Timestamp = DateTime.UtcNow,
                EventType = "TimelineItemCompleted",
                Description = $"Timeline item #{item.Number} '{item.Description}' marked {dto.Status} by {item.CompletedBy}",
                Data = JsonSerializer.Serialize(new { itemId, status = dto.Status, completedBy = item.CompletedBy, notes = dto.Notes }),
                Severity = dto.Status == "Failed" ? "Warning" : "Info"
            });

            await _context.SaveChangesAsync(ct);

            await BroadcastExecutionUpdateAsync(executionId, "TimelineItemUpdate", new { itemId, status = dto.Status });
            await CheckAndAutoCompleteExecutionAsync(executionId, ct);

            return item;
        }

        public async Task<ExecutionTimelineItem> ReportTimelineItemResultAsync(int executionId, int itemId, JsonElement resultData, CancellationToken ct)
        {
            var item = await ExecutionTimelineItems
                .FirstOrDefaultAsync(ti => ti.Id == itemId && ti.ExecutionId == executionId, ct);
            if (item == null)
                throw new InvalidOperationException("Timeline item not found");

            item.Status = "Completed";
            item.ResultData = resultData.GetRawText();
            item.CompletedBy = "client-report";
            item.CompletedAt = DateTime.UtcNow;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = executionId,
                Timestamp = DateTime.UtcNow,
                EventType = "TimelineItemCompleted",
                Description = $"Timeline item #{item.Number} completed via client report",
                Data = resultData.GetRawText(),
                Severity = "Info"
            });

            await _context.SaveChangesAsync(ct);

            await BroadcastExecutionUpdateAsync(executionId, "TimelineItemUpdate", new { itemId, status = "Completed" });
            await CheckAndAutoCompleteExecutionAsync(executionId, ct);

            return item;
        }

        private async Task BroadcastExecutionUpdateAsync(int executionId, string updateType, object data)
        {
            try
            {
                var connections = ExecutionHub.GetConnections();
                foreach (var connectionId in connections.GetConnections(executionId.ToString()))
                {
                    await _executionHub.Clients.Client(connectionId)
                        .SendAsync("ExecutionUpdate", new { executionId, updateType, data, timestamp = DateTime.UtcNow });
                }
                foreach (var connectionId in connections.GetConnections("all"))
                {
                    await _executionHub.Clients.Client(connectionId)
                        .SendAsync("ExecutionUpdate", new { executionId, updateType, data, timestamp = DateTime.UtcNow });
                }
            }
            catch (Exception ex)
            {
                _log.Warn($"Failed to broadcast execution update: {ex.Message}");
            }
        }

        private static readonly string[] TerminalItemStatuses = ["Completed", "Failed", "Skipped"];

        private async Task CheckAndAutoCompleteExecutionAsync(int executionId, CancellationToken ct)
        {
            var execution = await Executions.FindAsync(executionId);
            if (execution == null || execution.Status != ExecutionStatus.Running)
                return;

            var items = await ExecutionTimelineItems
                .Where(ti => ti.ExecutionId == executionId)
                .ToListAsync(ct);

            if (items.Count == 0) return;

            var allTerminal = items.All(ti => TerminalItemStatuses.Contains(ti.Status));
            if (!allTerminal) return;

            execution.Status = ExecutionStatus.Completed;
            execution.CompletedAt = DateTime.UtcNow;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = executionId,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution auto-completed: all timeline items are in terminal state",
                Data = JsonSerializer.Serialize(new { status = "Completed", timestamp = DateTime.UtcNow }),
                Severity = "Info"
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"Execution {executionId} auto-completed: all {items.Count} timeline items terminal");
            await BroadcastExecutionUpdateAsync(executionId, "StatusChange", new { status = "Completed" });
        }
    }
}
