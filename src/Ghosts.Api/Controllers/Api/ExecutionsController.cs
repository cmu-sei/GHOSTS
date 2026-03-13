// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ExecutionsController : ControllerBase
{
    private readonly IExecutionService _executionService;
    private readonly ILogger<ExecutionsController> _logger;

    public ExecutionsController(IExecutionService executionService, ILogger<ExecutionsController> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }

    // GET: api/executions
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ExecutionSummaryDto>>> GetExecutions(
        [FromQuery] int? scenarioId,
        CancellationToken ct)
    {
        try
        {
            var executions = await _executionService.GetAllAsync(scenarioId, ct);
            return Ok(executions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting executions");
            return StatusCode(500, new { error = "Error retrieving executions" });
        }
    }

    // GET: api/executions/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ExecutionDto>> GetExecution(int id, CancellationToken ct)
    {
        try
        {
            var execution = await _executionService.GetByIdAsync(id, ct);
            return Ok(MapToDto(execution));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error retrieving execution" });
        }
    }

    // GET: api/executions/5/events
    [HttpGet("{id}/events")]
    public async Task<ActionResult<IEnumerable<ExecutionEventDto>>> GetExecutionEvents(
        int id,
        [FromQuery] string eventType = null,
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
    {
        try
        {
            var events = await _executionService.GetEventsAsync(id, eventType, limit, ct);

            return Ok(events.Select(e => new ExecutionEventDto(
                e.Id,
                e.Timestamp,
                e.EventType,
                e.Description,
                e.Data,
                e.Severity
            )));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting events for execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error retrieving execution events" });
        }
    }

    // GET: api/executions/5/metrics
    [HttpGet("{id}/metrics")]
    public async Task<ActionResult<IEnumerable<ExecutionMetricSnapshotDto>>> GetExecutionMetrics(
        int id,
        [FromQuery] int? limit = 100,
        CancellationToken ct = default)
    {
        try
        {
            var snapshots = await _executionService.GetMetricsAsync(id, limit, ct);

            return Ok(snapshots.Select(ms => new ExecutionMetricSnapshotDto(
                ms.Id,
                ms.Timestamp,
                ms.ElapsedSeconds,
                ms.Metrics
            )));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics for execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error retrieving execution metrics" });
        }
    }

    // POST: api/executions
    [HttpPost]
    public async Task<ActionResult<ExecutionDto>> CreateExecution(CreateExecutionDto dto, CancellationToken ct)
    {
        try
        {
            var execution = await _executionService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetExecution), new { id = execution.Id }, MapToDto(execution));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating execution");
            return StatusCode(500, new { error = "Error creating execution" });
        }
    }

    // PUT: api/executions/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateExecution(int id, UpdateExecutionDto dto, CancellationToken ct)
    {
        try
        {
            await _executionService.UpdateAsync(id, dto, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error updating execution" });
        }
    }

    // DELETE: api/executions/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteExecution(int id, CancellationToken ct)
    {
        try
        {
            await _executionService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            // Check if it's a "cannot delete" error vs "not found" error
            if (ex.Message.Contains("Cannot delete"))
            {
                return BadRequest(new { error = ex.Message });
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error deleting execution" });
        }
    }

    // POST: api/executions/5/start
    [HttpPost("{id}/start")]
    public async Task<ActionResult<ExecutionDto>> StartExecution(int id, CancellationToken ct)
    {
        try
        {
            var execution = await _executionService.StartAsync(id, ct);
            return Ok(MapToDto(execution));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("Cannot start"))
            {
                return BadRequest(new { error = ex.Message });
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error starting execution" });
        }
    }

    // POST: api/executions/5/pause
    [HttpPost("{id}/pause")]
    public async Task<ActionResult<ExecutionDto>> PauseExecution(int id, CancellationToken ct)
    {
        try
        {
            var execution = await _executionService.PauseAsync(id, ct);
            return Ok(MapToDto(execution));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("Can only pause"))
            {
                return BadRequest(new { error = ex.Message });
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error pausing execution" });
        }
    }

    // POST: api/executions/5/stop
    [HttpPost("{id}/stop")]
    public async Task<ActionResult<ExecutionDto>> StopExecution(int id, [FromQuery] bool failed = false, CancellationToken ct = default)
    {
        try
        {
            var execution = await _executionService.StopAsync(id, failed, ct);
            return Ok(MapToDto(execution));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already in terminal state"))
            {
                return BadRequest(new { error = ex.Message });
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error stopping execution" });
        }
    }

    // POST: api/executions/5/cancel
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<ExecutionDto>> CancelExecution(int id, CancellationToken ct)
    {
        try
        {
            var execution = await _executionService.CancelAsync(id, ct);
            return Ok(MapToDto(execution));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already in terminal state"))
            {
                return BadRequest(new { error = ex.Message });
            }
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error cancelling execution" });
        }
    }

    // POST: api/executions/5/events
    [HttpPost("{id}/events")]
    public async Task<ActionResult<ExecutionEventDto>> AddExecutionEvent(int id, CreateExecutionEventDto dto, CancellationToken ct)
    {
        try
        {
            var executionEvent = await _executionService.AddEventAsync(id, dto, ct);

            return CreatedAtAction(
                nameof(GetExecutionEvents),
                new { id },
                new ExecutionEventDto(
                    executionEvent.Id,
                    executionEvent.Timestamp,
                    executionEvent.EventType,
                    executionEvent.Description,
                    executionEvent.Data,
                    executionEvent.Severity
                )
            );
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding event to execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error adding execution event" });
        }
    }

    // POST: api/executions/5/metrics
    [HttpPost("{id}/metrics")]
    public async Task<ActionResult<ExecutionMetricSnapshotDto>> AddMetricSnapshot(int id, [FromBody] JsonElement metricsData, CancellationToken ct)
    {
        try
        {
            var snapshot = await _executionService.AddMetricSnapshotAsync(id, metricsData, ct);

            return CreatedAtAction(
                nameof(GetExecutionMetrics),
                new { id },
                new ExecutionMetricSnapshotDto(
                    snapshot.Id,
                    snapshot.Timestamp,
                    snapshot.ElapsedSeconds,
                    snapshot.Metrics
                )
            );
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding metric snapshot to execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error adding metric snapshot" });
        }
    }

    // PUT: api/executions/5/metrics (update current metrics in execution)
    [HttpPut("{id}/metrics")]
    public async Task<IActionResult> UpdateExecutionMetrics(int id, [FromBody] JsonElement metricsData, CancellationToken ct)
    {
        try
        {
            await _executionService.UpdateMetricsAsync(id, metricsData, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating metrics for execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error updating execution metrics" });
        }
    }

    // PUT: api/executions/5/error (set error details)
    [HttpPut("{id}/error")]
    public async Task<IActionResult> SetExecutionError(int id, [FromBody] JsonElement errorData, CancellationToken ct)
    {
        try
        {
            await _executionService.SetErrorAsync(id, errorData, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting error details for execution {ExecutionId}", id);
            return StatusCode(500, new { error = "Error setting execution error" });
        }
    }

    // Helper methods
    private static ExecutionDto MapToDto(Execution execution)
    {
        return new ExecutionDto(
            execution.Id,
            execution.ScenarioId,
            execution.Scenario.Name ?? "Unknown",
            execution.Name,
            execution.Description,
            execution.Status.ToString(),
            execution.CreatedAt,
            execution.StartedAt,
            execution.CompletedAt,
            execution.ParameterOverrides,
            execution.Configuration,
            execution.Metrics,
            execution.ErrorDetails
        );
    }
}
