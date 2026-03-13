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

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioService
    {
        Task<List<Scenario>> GetAllAsync(CancellationToken ct);
        Task<Scenario> GetByIdAsync(int id, CancellationToken ct);
        Task<Scenario> CreateAsync(CreateScenarioDto dto, CancellationToken ct);
        Task<Scenario> UpdateAsync(int id, UpdateScenarioDto dto, CancellationToken ct);
        Task DeleteAsync(int id, CancellationToken ct);
    }

    public class ScenarioService(ApplicationDbContext context) : IScenarioService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<List<Scenario>> GetAllAsync(CancellationToken ct)
        {
            return await _context.Scenarios
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Nations)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.ThreatActors)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Injects)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.UserPools)
                .Include(s => s.TechnicalEnvironment)
                    .ThenInclude(te => te.Vulnerabilities)
                .Include(s => s.GameMechanics)
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .OrderByDescending(s => s.UpdatedAt)
                .ToListAsync(ct);
        }

        public async Task<Scenario> GetByIdAsync(int id, CancellationToken ct)
        {
            var scenario = await _context.Scenarios
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Nations)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.ThreatActors)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Injects)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.UserPools)
                .Include(s => s.TechnicalEnvironment)
                    .ThenInclude(te => te.Vulnerabilities)
                .Include(s => s.GameMechanics)
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (scenario == null)
            {
                _log.Error($"Scenario not found: {id}");
                throw new InvalidOperationException("Scenario not found");
            }

            return scenario;
        }

        public async Task<Scenario> CreateAsync(CreateScenarioDto dto, CancellationToken ct)
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

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create scenario: {operation}");
                throw new InvalidOperationException("Could not create Scenario");
            }

            _log.Info($"Created scenario: {scenario.Id} - {scenario.Name}");

            // Reload with all relationships
            return await GetByIdAsync(scenario.Id, ct);
        }

        public async Task<Scenario> UpdateAsync(int id, UpdateScenarioDto dto, CancellationToken ct)
        {
            var scenario = await _context.Scenarios
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Nations)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.ThreatActors)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.Injects)
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.UserPools)
                .Include(s => s.TechnicalEnvironment)
                    .ThenInclude(te => te.Vulnerabilities)
                .Include(s => s.GameMechanics)
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .FirstOrDefaultAsync(s => s.Id == id, ct);

            if (scenario == null)
            {
                _log.Error($"Scenario not found: {id}");
                throw new InvalidOperationException("Scenario not found");
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
                    scenario.TechnicalEnvironment.Defenses = System.Text.Json.JsonSerializer.Serialize(dto.TechnicalEnvironment.Defenses);

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

            await _context.SaveChangesAsync(ct);

            _log.Info($"Updated scenario: {scenario.Id} - {scenario.Name}");

            return scenario;
        }

        public async Task DeleteAsync(int id, CancellationToken ct)
        {
            var scenario = await _context.Scenarios.FindAsync(id);
            if (scenario == null)
            {
                _log.Error($"Scenario not found: {id}");
                throw new InvalidOperationException("Scenario not found");
            }

            _context.Scenarios.Remove(scenario);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not delete scenario: {operation}");
                throw new InvalidOperationException("Could not delete Scenario");
            }

            _log.Info($"Deleted scenario: {id}");
        }

        // Helper mapping methods
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
                Defenses = System.Text.Json.JsonSerializer.Serialize(dto.Defenses),
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
}
