// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ObjectivesController(IObjectiveService objectiveService, ILogger<ObjectivesController> logger) : ControllerBase
{
    private readonly IObjectiveService _objectiveService = objectiveService;
    private readonly ILogger<ObjectivesController> _logger = logger;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ObjectiveDto>>> GetObjectives([FromQuery] int? scenarioId, CancellationToken ct)
    {
        try
        {
            List<Objective> objectives;
            if (scenarioId.HasValue)
                objectives = await _objectiveService.GetByScenarioIdAsync(scenarioId.Value, ct);
            else
                objectives = await _objectiveService.GetAllAsync(ct);

            return Ok(objectives.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting objectives");
            return StatusCode(500, new { error = "Error retrieving objectives" });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ObjectiveDto>> GetObjective(int id, CancellationToken ct)
    {
        try
        {
            var objective = await _objectiveService.GetByIdAsync(id, ct);
            return Ok(MapToDto(objective));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting objective {ObjectiveId}", id);
            return StatusCode(500, new { error = "Error retrieving objective" });
        }
    }

    [HttpPost]
    public async Task<ActionResult<ObjectiveDto>> CreateObjective(CreateObjectiveDto dto, CancellationToken ct)
    {
        try
        {
            var objective = await _objectiveService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetObjective), new { id = objective.Id }, MapToDto(objective));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating objective");
            return StatusCode(500, new { error = "Error creating objective" });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ObjectiveDto>> UpdateObjective(int id, UpdateObjectiveDto dto, CancellationToken ct)
    {
        try
        {
            var objective = await _objectiveService.UpdateAsync(id, dto, ct);
            return Ok(MapToDto(objective));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating objective {ObjectiveId}", id);
            return StatusCode(500, new { error = "Error updating objective" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteObjective(int id, CancellationToken ct)
    {
        try
        {
            await _objectiveService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting objective {ObjectiveId}", id);
            return StatusCode(500, new { error = "Error deleting objective" });
        }
    }

    private static ObjectiveDto MapToDto(Objective o)
    {
        return new ObjectiveDto(
            o.Id,
            o.ParentId,
            o.ScenarioId,
            o.Name,
            o.Description,
            o.Type,
            o.Status,
            o.Score,
            o.Priority,
            o.SuccessCriteria,
            o.Assigned,
            o.SortOrder,
            o.CreatedAt,
            o.UpdatedAt,
            o.Children?.Select(MapToDto).ToList() ?? new List<ObjectiveDto>()
        );
    }
}
