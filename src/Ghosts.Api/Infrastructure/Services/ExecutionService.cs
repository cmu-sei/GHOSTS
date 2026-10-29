// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
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
        Task<ExecutionTimelineItem> RecordScenarioActionAsync(int executionId, int? itemId, int? eventNumber, string detail, CancellationToken ct);
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
        private DbSet<ScenarioTimelineEvent> ScenarioTimelineEvents => _context.Set<ScenarioTimelineEvent>();
        private DbSet<Inject> Injects => _context.Set<Inject>();
        private DbSet<ScenarioParameters> ScenarioParameters => _context.Set<ScenarioParameters>();
        private DbSet<NpcRecord> Npcs => _context.Set<NpcRecord>();
        private DbSet<ScenarioWorkflowBinding> ScenarioWorkflowBindings => _context.Set<ScenarioWorkflowBinding>();

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

            // Only a published scenario can be deployed; a draft is unreviewed work (I2)
            if (scenario.PublishedAt == null)
            {
                throw new InvalidOperationException($"Scenario {scenario.Name} is a draft. Publish it before running it.");
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

            foreach (var evt in scenarioEvents)
            {
                execution.TimelineItems.Add(new ExecutionTimelineItem
                {
                    SourceTimelineEventId = evt.Id,
                    Time = evt.Time,
                    Number = evt.Number,
                    Assigned = evt.Assigned,
                    Description = evt.Description,
                    Status = "Pending",
                    AutomationKind = AutomationKindFor(evt.ExecutionType, evt.Assigned),
                    WorkflowId = evt.WorkflowId,
                    TriggerKind = evt.TriggerKind,
                    Schedule = evt.Schedule,
                    TriggerCondition = evt.TriggerCondition,
                    Extras = evt.Extras,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var nextNumber = scenarioEvents.Count > 0 ? scenarioEvents.Max(e => e.Number) + 1 : 1;

            // ── Snapshot injects: the document's title-only events, numbered after the events ──
            var injects = await Injects
                .Where(i => i.ScenarioParameters.ScenarioId == dto.ScenarioId)
                .OrderBy(i => i.Id)
                .ToListAsync(ct);

            foreach (var inject in injects)
            {
                var extras = ScenarioExtras.Read<EventExtrasDto>(inject.Extras);
                var assigned = string.IsNullOrEmpty(extras?.Owner) ? "White Cell" : ScenarioDocumentMapper.CellName(extras.Owner);
                var mode = Enum.TryParse<ExecutionType>(extras?.Execution?.Mode, true, out var parsed) ? parsed : ExecutionType.Manual;
                execution.TimelineItems.Add(new ExecutionTimelineItem
                {
                    Time = inject.Trigger,
                    Number = nextNumber++,
                    Assigned = assigned,
                    Description = inject.Title,
                    Status = "Pending",
                    AutomationKind = AutomationKindFor(mode, assigned),
                    WorkflowId = extras?.Execution?.WorkflowRef,
                    TriggerKind = !string.IsNullOrEmpty(extras?.Schedule) ? TriggerKind.Scheduled
                        : !string.IsNullOrEmpty(extras?.When) ? TriggerKind.Triggered : TriggerKind.PointInTime,
                    Schedule = extras?.Schedule,
                    TriggerCondition = extras?.When,
                    Extras = inject.Extras,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // ── Snapshot default workflow bindings as scheduled Workflow items ──
            // These animate the run's NPC population (beliefs, social graph, posting, …)
            // on their cron without any hand-authored timeline event.
            var bindings = await ScenarioWorkflowBindings
                .Where(wb => wb.ScenarioParameters.ScenarioId == dto.ScenarioId && wb.Enabled)
                .ToListAsync(ct);

            foreach (var binding in bindings)
            {
                execution.TimelineItems.Add(new ExecutionTimelineItem
                {
                    Time = "T+0m",
                    Number = nextNumber++,
                    Assigned = "White Cell",
                    Description = $"Animation workflow: {binding.DisplayName}",
                    Status = "Pending",
                    AutomationKind = "Workflow",
                    WorkflowId = binding.WorkflowRef,
                    TriggerKind = TriggerKind.Scheduled,
                    Schedule = binding.Cron,
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

            // ── Generate NPC pools from the scenario's UserPools for this run ──
            await GenerateUserPoolNpcsAsync(execution, scenario.Name, ct);

            return await GetByIdAsync(execution.Id, ct);
        }

        /// <summary>
        /// Who carries out an item, decided once from its mode and owner: a workflow item goes to n8n; an npc item
        /// (1.2.0) goes to its targeted NPCs; otherwise a Green Cell or unowned item is the simulated population's, so
        /// it is dispatched to bound NPCs, and anything a cell owns (White Cell, Red Team, Blue Team) is done by a person.
        /// </summary>
        public static string AutomationKindFor(ExecutionType type, string assigned)
        {
            if (type == ExecutionType.Workflow) return "Workflow";
            if (type == ExecutionType.Npc) return "MachineUpdate";
            var cell = (assigned ?? string.Empty).Trim();
            return cell is "" or "None" or "Green Cell" ? "MachineUpdate" : "Manual";
        }

        /// <summary>
        /// Generates an execution-scoped population of NPCs for each UserPool(Role, Count)
        /// defined on the scenario. NPCs are grouped so each run has its own set:
        /// Campaign = scenario name, Enclave = execution run name, Team = pool role.
        /// These are separate from the compiler's Person-entity NPCs (scenario-scoped only).
        /// </summary>
        private async Task GenerateUserPoolNpcsAsync(Execution execution, string scenarioName, CancellationToken ct)
        {
            var pools = await ScenarioParameters
                .Where(sp => sp.ScenarioId == execution.ScenarioId)
                .SelectMany(sp => sp.UserPools)
                .ToListAsync(ct);

            if (pools.Count == 0) return;

            var totalCreated = 0;
            foreach (var pool in pools.Where(p => p.Count > 0))
            {
                var created = new List<NpcRecord>();
                for (var i = 0; i < pool.Count; i++)
                {
                    try
                    {
                        var npc = NpcRecord.TransformToNpc(Npc.Generate(MilitaryUnits.GetServiceBranch()));
                        npc.Id = npc.NpcProfile.Id;
                        npc.CreatedUtc = DateTime.UtcNow;
                        npc.ScenarioId = execution.ScenarioId;
                        npc.ExecutionId = execution.Id;
                        npc.Campaign = scenarioName;
                        npc.Enclave = execution.Name;
                        npc.Team = pool.Role;

                        Npcs.Add(npc);
                        created.Add(npc);
                    }
                    catch (Exception ex)
                    {
                        _log.Warn(ex, $"[Execution {execution.Id}] Failed to generate pool NPC {i + 1}/{pool.Count} for role '{pool.Role}'");
                    }
                }

                NpcCohortLinker.Link(_context, created);

                totalCreated += created.Count;
                _log.Info($"[Execution {execution.Id}] Generated {created.Count}/{pool.Count} NPCs for pool '{pool.Role}'");
            }

            if (totalCreated == 0) return;

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "UserPoolsGenerated",
                Description = $"Generated {totalCreated} NPC(s) across {pools.Count(p => p.Count > 0)} user pool(s)",
                Data = JsonSerializer.Serialize(new
                {
                    pools = pools.Select(p => new { role = p.Role, count = p.Count })
                }),
                Severity = "Info"
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"[Execution {execution.Id}] Generated {totalCreated} pool NPC(s) total");
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

        /// <summary>
        /// Starts or resumes a run. Nothing is sent to clients here: ExecutionDispatcher sends each item when it
        /// comes due, so a resume replays nothing. Resuming adds the pause to PausedSeconds so the clock skips it.
        /// </summary>
        public async Task<Execution> StartAsync(int id, CancellationToken ct)
        {
            var execution = await GetByIdAsync(id, ct);

            if (execution.Status != ExecutionStatus.Created && execution.Status != ExecutionStatus.Paused)
            {
                _log.Error($"Cannot start execution with status {execution.Status}: {id}");
                throw new InvalidOperationException($"Cannot start execution with status {execution.Status}");
            }

            var now = DateTime.UtcNow;
            var resuming = execution.Status == ExecutionStatus.Paused;
            if (resuming && execution.PausedAt.HasValue)
                execution.PausedSeconds += Math.Max(0, (now - execution.PausedAt.Value).TotalSeconds);
            execution.PausedAt = null;

            var timelineItems = await ExecutionTimelineItems
                .Where(ti => ti.ExecutionId == id)
                .ToListAsync(ct);

            foreach (var item in timelineItems.Where(ti => ti.AutomationKind == "Workflow" && ti.Status == "Pending"))
                item.Status = "Queued";

            execution.Status = ExecutionStatus.Running;
            execution.StartedAt ??= now;

            // A first start seeds the run's ground truth from the document's starting conditions.
            if (!resuming && string.IsNullOrEmpty(execution.RunState))
            {
                var extras = ScenarioExtras.Read<ScenarioExtrasDto>(execution.Scenario?.Extras
                    ?? await Scenarios.Where(s => s.Id == execution.ScenarioId).Select(s => s.Extras).FirstOrDefaultAsync(ct));
                RunState.Write(execution, RunState.Seed(extras?.StartingConditions));
            }

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = now,
                EventType = "StatusChange",
                Description = resuming ? "Execution resumed" : "Execution started",
                Data = JsonSerializer.Serialize(new
                {
                    status = "Running",
                    timestamp = now,
                    pausedSeconds = execution.PausedSeconds,
                    timelineItemsTotal = timelineItems.Count,
                    automatedItems = timelineItems.Count(ti => ti.AutomationKind == "MachineUpdate"),
                    workflowItems = timelineItems.Count(ti => ti.AutomationKind == "Workflow"),
                    manualItems = timelineItems.Count(ti => ti.AutomationKind == "Manual")
                }),
                Severity = "Info"
            });

            await _context.SaveChangesAsync(ct);
            _log.Info($"{(resuming ? "Resumed" : "Started")} execution {id}");
            await BroadcastExecutionUpdateAsync(id, "StatusChange", new { status = "Running" });

            return execution;
        }

        /// <summary>Parses "T+15m" → 900000 ms, and the schema's "T+2d9h40m" form the way the validator does. Returns 0 for any unparseable input.</summary>
        public static int ParseTimeOffsetMs(string? time)
        {
            if (string.IsNullOrWhiteSpace(time)) return 0;
            if (ScenarioDocumentMapper.TryOffsetMinutes(time.Trim(), out var minutes)) return minutes * 60_000;
            var m = Regex.Match(time, @"T\+(\d+)m", RegexOptions.IgnoreCase);
            if (!m.Success) return 0;
            return int.Parse(m.Groups[1].Value) * 60_000;
        }

        /// <summary>Minutes from T+0 for an offset; 0 for anything unparseable. Long, so offsets past 24 days don't overflow.</summary>
        public static long ParseTimeOffsetMinutes(string time)
        {
            if (string.IsNullOrWhiteSpace(time)) return 0;
            if (ScenarioDocumentMapper.TryOffsetMinutes(time.Trim(), out var minutes)) return minutes;
            var m = Regex.Match(time, @"T\+(\d+)m", RegexOptions.IgnoreCase);
            return m.Success && long.TryParse(m.Groups[1].Value, out var legacy) ? legacy : 0;
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
            execution.PausedAt = DateTime.UtcNow;
            await ExecutionDispatcher.WithdrawQueuedAsync(_context, execution.Id, ct);

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
            await ExecutionDispatcher.WithdrawQueuedAsync(_context, execution.Id, ct);

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
            await ExecutionDispatcher.WithdrawQueuedAsync(_context, execution.Id, ct);

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
            await ExecutionDispatcher.WithdrawQueuedAsync(_context, execution.Id, ct);

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
            if (dto.Status == "Completed") await ApplyEffectsAsync(executionId, item, ct);

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

            var wasOpen = !TerminalItemStatuses.Contains(item.Status);
            item.Status = "Completed";
            item.ResultData = resultData.GetRawText();
            item.CompletedBy = "client-report";
            item.CompletedAt = DateTime.UtcNow;
            if (wasOpen) await ApplyEffectsAsync(executionId, item, ct);

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

        /// <summary>
        /// Advances the execution timeline item matching a client-reported npc-scenario-action.
        /// Point-in-time/triggered items complete on first report; scheduled (looping) items
        /// record a fire and stay active so they can recur. Closes the loop so scenario events
        /// reported by NPC clients drive execution progress instead of remaining metadata.
        /// </summary>
        public async Task<ExecutionTimelineItem> RecordScenarioActionAsync(int executionId, int? itemId, int? eventNumber, string detail, CancellationToken ct)
        {
            var item = itemId.HasValue
                ? await ExecutionTimelineItems.FirstOrDefaultAsync(ti => ti.ExecutionId == executionId && ti.Id == itemId.Value, ct)
                : await ExecutionTimelineItems.FirstOrDefaultAsync(ti => ti.ExecutionId == executionId && ti.Number == eventNumber, ct);
            if (item == null)
                throw new InvalidOperationException("Timeline item not found");

            // A report that arrives after the run ended is late news, not progress.
            var execution = await Executions.FindAsync([executionId], ct);
            if (execution == null || (execution.Status != ExecutionStatus.Running && execution.Status != ExecutionStatus.Paused))
            {
                _log.Debug($"Ignoring scenario action for item #{item.Number}: execution {executionId} is {execution?.Status}");
                return item;
            }

            item.LastFiredAt = DateTime.UtcNow;
            item.FireCount++;

            // Scheduled items loop; everything else completes on the first reported action.
            var completed = item.TriggerKind != TriggerKind.Scheduled;
            if (completed)
            {
                var wasOpen = !TerminalItemStatuses.Contains(item.Status);
                item.Status = "Completed";
                item.CompletedBy = "client-report";
                item.CompletedAt = DateTime.UtcNow;
                if (wasOpen) await RunState.ApplyItemEffectsAsync(_context, execution, item, ct);
            }

            ExecutionEvents.Add(new ExecutionEvent
            {
                ExecutionId = executionId,
                Timestamp = DateTime.UtcNow,
                EventType = "ScenarioActionReported",
                Description = $"Scenario action for timeline item #{item.Number} reported by client (fire #{item.FireCount})",
                Data = JsonSerializer.Serialize(new { itemId = item.Id, eventNumber = item.Number, detail, fireCount = item.FireCount, completed }),
                Severity = "Info"
            });

            await _context.SaveChangesAsync(ct);

            await BroadcastExecutionUpdateAsync(executionId, "TimelineItemUpdate", new { itemId = item.Id, status = item.Status });
            if (completed)
                await CheckAndAutoCompleteExecutionAsync(executionId, ct);

            return item;
        }

        /// <summary>Applies a completed item's event effects to its run's state, once.</summary>
        private async Task ApplyEffectsAsync(int executionId, ExecutionTimelineItem item, CancellationToken ct)
        {
            var execution = await Executions.FindAsync([executionId], ct);
            if (execution != null) await RunState.ApplyItemEffectsAsync(_context, execution, item, ct);
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
            await ExecutionDispatcher.WithdrawQueuedAsync(_context, executionId, ct);

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
