// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ScenariosController> _logger;

    public ScenariosController(ApplicationDbContext context, ILogger<ScenariosController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/scenarios
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ScenarioDto>>> GetScenarios()
    {
        var scenarios = await _context.Scenarios
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.Nations)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.ThreatActors)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.Injects)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.UserPools)
            .Include(s => s.TechnicalEnvironment)
                .ThenInclude(te => te!.Vulnerabilities)
            .Include(s => s.GameMechanics)
            .Include(s => s.ScenarioTimeline)
                .ThenInclude(t => t!.ScenarioTimelineEvents)
            .ToListAsync();

        return Ok(scenarios.Select(MapToDto));
    }

    // GET: api/scenarios/5
    [HttpGet("{id}")]
    public async Task<ActionResult<ScenarioDto>> GetScenario(int id)
    {
        var scenario = await _context.Scenarios
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.Nations)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.ThreatActors)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.Injects)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.UserPools)
            .Include(s => s.TechnicalEnvironment)
                .ThenInclude(te => te!.Vulnerabilities)
            .Include(s => s.GameMechanics)
            .Include(s => s.ScenarioTimeline)
                .ThenInclude(t => t!.ScenarioTimelineEvents)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (scenario == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(scenario));
    }

    // POST: api/scenarios
    [HttpPost]
    public async Task<ActionResult<ScenarioDto>> CreateScenario(CreateScenarioDto dto)
    {
        var scenario = new Scenario
        {
            Name = dto.Name,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (dto.ScenarioParameters != null)
        {
            scenario.ScenarioParameters = MapScenarioParameters(dto.ScenarioParameters);
        }

        if (dto.TechnicalEnvironment != null)
        {
            scenario.TechnicalEnvironment = MapTechnicalEnvironment(dto.TechnicalEnvironment);
        }

        if (dto.GameMechanics != null)
        {
            scenario.GameMechanics = MapGameMechanics(dto.GameMechanics);
        }

        if (dto.Timeline != null)
        {
            scenario.ScenarioTimeline = MapTimeline(dto.Timeline);
        }

        _context.Scenarios.Add(scenario);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetScenario), new { id = scenario.Id }, MapToDto(scenario));
    }

    // PUT: api/scenarios/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateScenario(int id, UpdateScenarioDto dto)
    {
        var scenario = await _context.Scenarios
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.Nations)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.ThreatActors)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.Injects)
            .Include(s => s.ScenarioParameters)
                .ThenInclude(sp => sp!.UserPools)
            .Include(s => s.TechnicalEnvironment)
                .ThenInclude(te => te!.Vulnerabilities)
            .Include(s => s.GameMechanics)
            .Include(s => s.ScenarioTimeline)
                .ThenInclude(t => t!.ScenarioTimelineEvents)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (scenario == null)
        {
            return NotFound();
        }

        scenario.Name = dto.Name;
        scenario.Description = dto.Description;
        scenario.UpdatedAt = DateTime.UtcNow;

        // Update ScenarioParameters
        if (dto.ScenarioParameters != null)
        {
            if (scenario.ScenarioParameters != null)
            {
                // Remove old collections
                _context.Nations.RemoveRange(scenario.ScenarioParameters.Nations);
                _context.ThreatActors.RemoveRange(scenario.ScenarioParameters.ThreatActors);
                _context.Injects.RemoveRange(scenario.ScenarioParameters.Injects);
                _context.UserPools.RemoveRange(scenario.ScenarioParameters.UserPools);

                // Update properties
                scenario.ScenarioParameters.Objectives = dto.ScenarioParameters.Objectives;
                scenario.ScenarioParameters.PoliticalContext = dto.ScenarioParameters.PoliticalContext;
                scenario.ScenarioParameters.RulesOfEngagement = dto.ScenarioParameters.RulesOfEngagement;
                scenario.ScenarioParameters.VictoryConditions = dto.ScenarioParameters.VictoryConditions;

                // Add new collections
                scenario.ScenarioParameters.Nations = dto.ScenarioParameters.Nations.Select(n => new Nation
                {
                    Name = n.Name,
                    Alignment = n.Alignment
                }).ToList();

                scenario.ScenarioParameters.ThreatActors = dto.ScenarioParameters.ThreatActors.Select(ta => new ThreatActor
                {
                    Name = ta.Name,
                    Type = ta.Type,
                    Capability = ta.Capability,
                    Ttps = string.Join(",", ta.Ttps)
                }).ToList();

                scenario.ScenarioParameters.Injects = dto.ScenarioParameters.Injects.Select(i => new Inject
                {
                    Trigger = i.Trigger,
                    Title = i.Title
                }).ToList();

                scenario.ScenarioParameters.UserPools = dto.ScenarioParameters.UserPools.Select(up => new UserPool
                {
                    Role = up.Role,
                    Count = up.Count
                }).ToList();
            }
            else
            {
                scenario.ScenarioParameters = MapScenarioParameters(dto.ScenarioParameters);
            }
        }

        // Update TechnicalEnvironment
        if (dto.TechnicalEnvironment != null)
        {
            if (scenario.TechnicalEnvironment != null)
            {
                _context.Vulnerabilities.RemoveRange(scenario.TechnicalEnvironment.Vulnerabilities);

                scenario.TechnicalEnvironment.NetworkTopology = dto.TechnicalEnvironment.NetworkTopology;
                scenario.TechnicalEnvironment.Services = dto.TechnicalEnvironment.Services;
                scenario.TechnicalEnvironment.Assets = dto.TechnicalEnvironment.Assets;
                scenario.TechnicalEnvironment.Defenses = JsonSerializer.Serialize(dto.TechnicalEnvironment.Defenses);

                scenario.TechnicalEnvironment.Vulnerabilities = dto.TechnicalEnvironment.Vulnerabilities.Select(v => new Vulnerability
                {
                    Asset = v.Asset,
                    Cve = v.Cve,
                    Severity = v.Severity
                }).ToList();
            }
            else
            {
                scenario.TechnicalEnvironment = MapTechnicalEnvironment(dto.TechnicalEnvironment);
            }
        }

        // Update GameMechanics
        if (dto.GameMechanics != null)
        {
            if (scenario.GameMechanics != null)
            {
                scenario.GameMechanics.TimelineType = dto.GameMechanics.TimelineType;
                scenario.GameMechanics.DurationHours = dto.GameMechanics.DurationHours;
                scenario.GameMechanics.AdjudicationType = dto.GameMechanics.AdjudicationType;
                scenario.GameMechanics.EscalationLadder = dto.GameMechanics.EscalationLadder;
                scenario.GameMechanics.BranchingLogic = dto.GameMechanics.BranchingLogic;
                scenario.GameMechanics.CollectLogs = dto.GameMechanics.Telemetry.CollectLogs;
                scenario.GameMechanics.CollectNetwork = dto.GameMechanics.Telemetry.CollectNetwork;
                scenario.GameMechanics.CollectEndpoint = dto.GameMechanics.Telemetry.CollectEndpoint;
                scenario.GameMechanics.CollectChat = dto.GameMechanics.Telemetry.CollectChat;
                scenario.GameMechanics.PerformanceMetrics = dto.GameMechanics.PerformanceMetrics;
            }
            else
            {
                scenario.GameMechanics = MapGameMechanics(dto.GameMechanics);
            }
        }

        // Update Timeline
        if (dto.Timeline != null)
        {
            if (scenario.ScenarioTimeline != null)
            {
                _context.ScenarioTimelineEvents.RemoveRange(scenario.ScenarioTimeline.ScenarioTimelineEvents);

                scenario.ScenarioTimeline.ExerciseDuration = dto.Timeline.ExerciseDuration;
                scenario.ScenarioTimeline.ScenarioTimelineEvents = dto.Timeline.Events.Select(e => new ScenarioTimelineEvent
                {
                    Time = e.Time,
                    Number = e.Number,
                    Assigned = e.Assigned,
                    Description = e.Description,
                    Status = e.Status
                }).ToList();
            }
            else
            {
                scenario.ScenarioTimeline = MapTimeline(dto.Timeline);
            }
        }

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/scenarios/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteScenario(int id)
    {
        var scenario = await _context.Scenarios.FindAsync(id);
        if (scenario == null)
        {
            return NotFound();
        }

        _context.Scenarios.Remove(scenario);
        await _context.SaveChangesAsync();

        return NoContent();
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

    private static ScenarioParameters MapScenarioParameters(ScenarioParametersDto dto)
    {
        return new ScenarioParameters
        {
            Objectives = dto.Objectives,
            PoliticalContext = dto.PoliticalContext,
            RulesOfEngagement = dto.RulesOfEngagement,
            VictoryConditions = dto.VictoryConditions,
            Nations = dto.Nations.Select(n => new Nation { Name = n.Name, Alignment = n.Alignment }).ToList(),
            ThreatActors = dto.ThreatActors.Select(ta => new ThreatActor
            {
                Name = ta.Name,
                Type = ta.Type,
                Capability = ta.Capability,
                Ttps = string.Join(",", ta.Ttps)
            }).ToList(),
            Injects = dto.Injects.Select(i => new Inject { Trigger = i.Trigger, Title = i.Title }).ToList(),
            UserPools = dto.UserPools.Select(up => new UserPool { Role = up.Role, Count = up.Count }).ToList()
        };
    }

    private static TechnicalEnvironment MapTechnicalEnvironment(TechnicalEnvironmentDto dto)
    {
        return new TechnicalEnvironment
        {
            NetworkTopology = dto.NetworkTopology,
            Services = dto.Services,
            Assets = dto.Assets,
            Defenses = JsonSerializer.Serialize(dto.Defenses),
            Vulnerabilities = dto.Vulnerabilities.Select(v => new Vulnerability
            {
                Asset = v.Asset,
                Cve = v.Cve,
                Severity = v.Severity
            }).ToList()
        };
    }

    private static GameMechanics MapGameMechanics(GameMechanicsDto dto)
    {
        return new GameMechanics
        {
            TimelineType = dto.TimelineType,
            DurationHours = dto.DurationHours,
            AdjudicationType = dto.AdjudicationType,
            EscalationLadder = dto.EscalationLadder,
            BranchingLogic = dto.BranchingLogic,
            CollectLogs = dto.Telemetry.CollectLogs,
            CollectNetwork = dto.Telemetry.CollectNetwork,
            CollectEndpoint = dto.Telemetry.CollectEndpoint,
            CollectChat = dto.Telemetry.CollectChat,
            PerformanceMetrics = dto.PerformanceMetrics
        };
    }

    private static ScenarioTimeline MapTimeline(TimelineDto dto)
    {
        return new ScenarioTimeline
        {
            ExerciseDuration = dto.ExerciseDuration,
            ScenarioTimelineEvents = dto.Events.Select(e => new ScenarioTimelineEvent
            {
                Time = e.Time,
                Number = e.Number,
                Assigned = e.Assigned,
                Description = e.Description,
                Status = e.Status
            }).ToList()
        };
    }
}
