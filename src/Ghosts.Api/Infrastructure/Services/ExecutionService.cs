// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
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

    public class ExecutionService(ApplicationDbContext context) : IExecutionService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<List<ExecutionSummaryDto>> GetAllAsync(int? scenarioId, CancellationToken ct)
        {
            var query = _context.Executions
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
            var execution = await _context.Executions
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
            // Verify scenario exists
            var scenario = await _context.Scenarios.FindAsync(dto.ScenarioId);
            if (scenario == null)
            {
                _log.Error($"Scenario not found: {dto.ScenarioId}");
                throw new InvalidOperationException($"Scenario with id {dto.ScenarioId} not found");
            }

            // Generate default name if not provided
            var executionCount = await _context.Executions.CountAsync(e => e.ScenarioId == dto.ScenarioId, ct);
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

            _context.Executions.Add(execution);

            // Add initial event
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

            // Reload with scenario
            return await GetByIdAsync(execution.Id, ct);
        }

        public async Task<Execution> UpdateAsync(int id, UpdateExecutionDto dto, CancellationToken ct)
        {
            var execution = await _context.Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            if (!string.IsNullOrEmpty(dto.Name))
            {
                execution.Name = dto.Name;
            }

            if (dto.Description != null)
            {
                execution.Description = dto.Description;
            }

            if (dto.ParameterOverrides != null)
            {
                execution.ParameterOverrides = dto.ParameterOverrides;
            }

            if (dto.Configuration != null)
            {
                execution.Configuration = dto.Configuration;
            }

            await _context.SaveChangesAsync(ct);

            _log.Info($"Updated execution: {id}");

            return await GetByIdAsync(id, ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var execution = await _context.Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            // Only allow deletion of non-active executions
            if (execution.Status == ExecutionStatus.Running || execution.Status == ExecutionStatus.Paused)
            {
                _log.Error($"Cannot delete running or paused execution: {id}");
                throw new InvalidOperationException("Cannot delete a running or paused execution. Stop it first.");
            }

            _context.Executions.Remove(execution);

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

            execution.Status = ExecutionStatus.Running;
            if (execution.StartedAt == null)
            {
                execution.StartedAt = DateTime.UtcNow;
            }

            // Add event
            var startEvent = new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution started",
                Data = JsonSerializer.Serialize(new { status = "Running", timestamp = DateTime.UtcNow }),
                Severity = "Info"
            };
            _context.ExecutionEvents.Add(startEvent);

            await _context.SaveChangesAsync(ct);

            _log.Info($"Started execution: {id}");

            return execution;
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

            // Add event
            var pauseEvent = new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution paused",
                Data = JsonSerializer.Serialize(new { status = "Paused", timestamp = DateTime.UtcNow }),
                Severity = "Info"
            };
            _context.ExecutionEvents.Add(pauseEvent);

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

            // Add event
            var stopEvent = new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = failed ? "Execution failed" : "Execution completed",
                Data = JsonSerializer.Serialize(new { status = execution.Status.ToString(), timestamp = DateTime.UtcNow }),
                Severity = failed ? "Error" : "Info"
            };
            _context.ExecutionEvents.Add(stopEvent);

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

            // Add event
            var cancelEvent = new ExecutionEvent
            {
                ExecutionId = execution.Id,
                Timestamp = DateTime.UtcNow,
                EventType = "StatusChange",
                Description = "Execution cancelled by user",
                Data = JsonSerializer.Serialize(new { status = "Cancelled", timestamp = DateTime.UtcNow }),
                Severity = "Warning"
            };
            _context.ExecutionEvents.Add(cancelEvent);

            await _context.SaveChangesAsync(ct);

            _log.Info($"Cancelled execution: {id}");

            return execution;
        }

        public async Task<List<ExecutionEvent>> GetEventsAsync(int id, string eventType, int? limit, CancellationToken ct)
        {
            var query = _context.ExecutionEvents
                .Where(e => e.ExecutionId == id)
                .OrderByDescending(e => e.Timestamp)
                .AsQueryable();

            if (!string.IsNullOrEmpty(eventType))
            {
                query = query.Where(e => e.EventType == eventType);
            }

            if (limit.HasValue && limit.Value > 0)
            {
                query = query.Take(limit.Value);
            }

            return await query.ToListAsync(ct);
        }

        public async Task<ExecutionEvent> AddEventAsync(int id, CreateExecutionEventDto dto, CancellationToken ct)
        {
            var execution = await _context.Executions.FindAsync(id);
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

            _context.ExecutionEvents.Add(executionEvent);
            await _context.SaveChangesAsync(ct);

            return executionEvent;
        }

        public async Task<List<ExecutionMetricSnapshot>> GetMetricsAsync(int id, int? limit, CancellationToken ct)
        {
            var query = _context.ExecutionMetricSnapshots
                .Where(ms => ms.ExecutionId == id)
                .OrderByDescending(ms => ms.Timestamp)
                .AsQueryable();

            if (limit.HasValue && limit.Value > 0)
            {
                query = query.Take(limit.Value);
            }

            return await query.ToListAsync(ct);
        }

        public async Task<ExecutionMetricSnapshot> AddMetricSnapshotAsync(int id, JsonElement metrics, CancellationToken ct)
        {
            var execution = await _context.Executions.FindAsync(id);
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

            _context.ExecutionMetricSnapshots.Add(snapshot);
            await _context.SaveChangesAsync(ct);

            return snapshot;
        }

        public async Task UpdateMetricsAsync(int id, JsonElement metrics, CancellationToken ct)
        {
            var execution = await _context.Executions.FindAsync(id);
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
            var execution = await _context.Executions.FindAsync(id);
            if (execution == null)
            {
                _log.Error($"Execution not found: {id}");
                throw new InvalidOperationException("Execution not found");
            }

            execution.ErrorDetails = error.GetRawText();
            execution.Status = ExecutionStatus.Failed;
            execution.CompletedAt = DateTime.UtcNow;

            // Add error event
            var errorEvent = new ExecutionEvent
            {
                ExecutionId = id,
                Timestamp = DateTime.UtcNow,
                EventType = "Error",
                Description = "Execution encountered an error",
                Data = error.GetRawText(),
                Severity = "Error"
            };
            _context.ExecutionEvents.Add(errorEvent);

            await _context.SaveChangesAsync(ct);

            _log.Error($"Execution {id} failed with error: {error.GetRawText()}");
        }
    }
}
