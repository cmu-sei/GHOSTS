// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly IScenarioService _scenarioService;
    private readonly INpcService _npcService;
    private readonly ILogger<ScenariosController> _logger;

    public ScenariosController(IScenarioService scenarioService, INpcService npcService, ILogger<ScenariosController> logger)
    {
        _scenarioService = scenarioService;
        _npcService = npcService;
        _logger = logger;
    }

    // GET: api/scenarios
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScenarioDto>>> GetScenarios(CancellationToken ct)
    {
        try
        {
            var scenarios = await _scenarioService.GetAllAsync(ct);
            return Ok(scenarios.Select(MapToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scenarios");
            return StatusCode(500, new { error = "Error retrieving scenarios" });
        }
    }

    // GET: api/scenarios/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ScenarioDto>> GetScenario(int id, CancellationToken ct)
    {
        try
        {
            var scenario = await _scenarioService.GetByIdAsync(id, ct);
            return Ok(MapToDto(scenario));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error retrieving scenario" });
        }
    }

    // POST: api/scenarios
    [HttpPost]
    public async Task<ActionResult<ScenarioDto>> CreateScenario(CreateScenarioDto dto, CancellationToken ct)
    {
        try
        {
            var scenario = await _scenarioService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetScenario), new { id = scenario.Id }, MapToDto(scenario));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating scenario");
            return StatusCode(500, new { error = "Error creating scenario" });
        }
    }

    // PUT: api/scenarios/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateScenario(int id, UpdateScenarioDto dto, CancellationToken ct)
    {
        try
        {
            await _scenarioService.UpdateAsync(id, dto, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error updating scenario" });
        }
    }

    // DELETE: api/scenarios/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteScenario(int id, CancellationToken ct)
    {
        try
        {
            await _scenarioService.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting scenario {ScenarioId}", id);
            return StatusCode(500, new { error = "Error deleting scenario" });
        }
    }

    /// <summary>
    /// Get all NPCs associated with a specific scenario
    /// </summary>
    /// <param name="scenarioId">The scenario ID</param>
    /// <returns>List of NPCs bound to the scenario</returns>
    [ProducesResponseType(typeof(ActionResult<IEnumerable<NpcRecord>>), (int)HttpStatusCode.OK)]
    [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(ActionResult<IEnumerable<NpcRecord>>))]
    [SwaggerOperation("GetScenarioNpcs")]
    [HttpGet("{scenarioId}/npcs")]
    public async Task<ActionResult<IEnumerable<NpcRecord>>> GetScenarioNpcs(int scenarioId)
    {
        try
        {
            var npcs = await _npcService.GetByScenarioId(scenarioId);
            return Ok(npcs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting NPCs for scenario {ScenarioId}", scenarioId);
            return StatusCode(500, new { error = "Error retrieving scenario NPCs" });
        }
    }

    // Helper methods
    private static ScenarioDto MapToDto(Scenario scenario)
    {
        return new ScenarioDto(
            scenario.Id,
            scenario.Name,
            scenario.Description,
            scenario.CreatedAt,
            scenario.UpdatedAt,
            scenario.ScenarioParameters != null ? new ScenarioParametersDto(
                scenario.ScenarioParameters.Nations.Select(n => new NationDto(n.Name, n.Alignment)).ToList(),
                scenario.ScenarioParameters.ThreatActors.Select(ta => new ThreatActorDto(
                    ta.Name,
                    ta.Type,
                    ta.Capability,
                    ta.Ttps?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>()
                )).ToList(),
                scenario.ScenarioParameters.Injects.Select(i => new InjectDto(i.Trigger, i.Title)).ToList(),
                scenario.ScenarioParameters.UserPools.Select(up => new UserPoolDto(up.Role, up.Count)).ToList(),
                scenario.ScenarioParameters.Objectives,
                scenario.ScenarioParameters.PoliticalContext,
                scenario.ScenarioParameters.RulesOfEngagement,
                scenario.ScenarioParameters.VictoryConditions
            ) : null,
            scenario.TechnicalEnvironment != null ? new TechnicalEnvironmentDto(
                scenario.TechnicalEnvironment.NetworkTopology,
                scenario.TechnicalEnvironment.Services,
                scenario.TechnicalEnvironment.Assets,
                JsonSerializer.Deserialize<List<string>>(scenario.TechnicalEnvironment.Defenses ?? "[]") ?? new List<string>(),
                scenario.TechnicalEnvironment.Vulnerabilities.Select(v => new VulnerabilityDto(v.Asset, v.Cve, v.Severity)).ToList()
            ) : null,
            scenario.GameMechanics != null ? new GameMechanicsDto(
                scenario.GameMechanics.TimelineType,
                scenario.GameMechanics.DurationHours,
                scenario.GameMechanics.AdjudicationType,
                scenario.GameMechanics.EscalationLadder,
                scenario.GameMechanics.BranchingLogic,
                new TelemetryDto(
                    scenario.GameMechanics.CollectLogs,
                    scenario.GameMechanics.CollectNetwork,
                    scenario.GameMechanics.CollectEndpoint,
                    scenario.GameMechanics.CollectChat
                ),
                scenario.GameMechanics.PerformanceMetrics
            ) : null,
            scenario.ScenarioTimeline != null ? new TimelineDto(
                scenario.ScenarioTimeline.ExerciseDuration,
                scenario.ScenarioTimeline.ScenarioTimelineEvents.Select(e => new TimelineEventDto(e.Time, e.Number, e.Assigned, e.Description, e.Status)).ToList()
            ) : null
        );
    }

}
