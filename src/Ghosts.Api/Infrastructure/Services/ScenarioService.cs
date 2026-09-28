// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioService
    {
        Task<List<Scenario>> GetAllAsync(CancellationToken ct);
        Task<Scenario> GetByIdAsync(int id, CancellationToken ct);
        Task<Scenario> CreateAsync(CreateScenarioDto dto, CancellationToken ct);
        Task<string> ExportDocumentAsync(int id, CancellationToken ct);
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
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.WorkflowBindings)
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
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.WorkflowBindings)
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

        /// <summary>
        /// The scenario as canonical scenario-document text. The graph and the objectives are read
        /// here because the scenario read path does not include them.
        /// </summary>
        public async Task<string> ExportDocumentAsync(int id, CancellationToken ct)
        {
            var scenario = await GetByIdAsync(id, ct);
            var entities = await _context.ScenarioEntities.Where(e => e.ScenarioId == id).ToListAsync(ct);
            var edges = await _context.ScenarioEdges.Where(e => e.ScenarioId == id).ToListAsync(ct);
            var objectives = await _context.Objectives.Where(o => o.ScenarioId == id).ToListAsync(ct);

            return ScenarioDocumentMapper.Serialize(
                ScenarioDocumentMapper.ToDocument(scenario, entities, edges, objectives));
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

            if (dto.Objectives != null)
            {
                scenario.Objectives = MapObjectives(dto.Objectives);
            }

            if (dto.Entities != null)
            {
                MapGraph(dto, scenario);
            }

            _context.Scenarios.Add(scenario);

            // One transaction: an objective's database id is only known after the first save, and
            // the parent and event references that point at it are rewritten in the second.
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create scenario: {operation}");
                throw new InvalidOperationException("Could not create Scenario");
            }

            if (scenario.Objectives.Count > 0)
            {
                RemapObjectiveIds(dto.Objectives, scenario);
                await _context.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);

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
                .Include(s => s.ScenarioParameters)
                    .ThenInclude(sp => sp.WorkflowBindings)
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
            if (dto.BuilderStatus != null)
                scenario.BuilderStatus = dto.BuilderStatus;

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

                    // Only replace workflow bindings when the client explicitly supplies them,
                    // so an older client omitting the field doesn't wipe seeded defaults.
                    if (dto.ScenarioParameters.WorkflowBindings != null)
                    {
                        _context.ScenarioWorkflowBindings.RemoveRange(scenario.ScenarioParameters.WorkflowBindings);
                        scenario.ScenarioParameters.WorkflowBindings = MapWorkflowBindings(dto.ScenarioParameters.WorkflowBindings);
                    }
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
                        Status = e.Status,
                        ObjectiveIds = e.ObjectiveIds?.Count > 0 ? JsonSerializer.Serialize(e.ObjectiveIds) : null,
                        TriggerKind = Enum.TryParse<TriggerKind>(e.TriggerKind, true, out var tk) ? tk : TriggerKind.PointInTime,
                        Schedule = e.Schedule,
                        TriggerCondition = e.TriggerCondition,
                        ExecutionType = Enum.TryParse<ExecutionType>(e.ExecutionType, true, out var et) ? et : ExecutionType.Manual,
                        WorkflowId = e.WorkflowId
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
                UserPools = dto.UserPools.Select(up => new UserPool { Role = up.Role, Count = up.Count }).ToList(),
                WorkflowBindings = MapWorkflowBindings(dto.WorkflowBindings)
            };
        }

        /// <summary>
        /// Maps binding DTOs to entities, or seeds the default animation workflow set when none
        /// are supplied — so a new scenario's runs come alive out of the box while staying overridable.
        /// </summary>
        private static List<ScenarioWorkflowBinding> MapWorkflowBindings(List<ScenarioWorkflowBindingDto> dtos)
        {
            if (dtos == null)
            {
                return ScenarioWorkflowBinding.Defaults()
                    .Select(d => new ScenarioWorkflowBinding
                    {
                        WorkflowRef = d.WorkflowRef,
                        DisplayName = d.DisplayName,
                        Cron = d.Cron,
                        Enabled = d.Enabled
                    }).ToList();
            }

            return dtos.Select(b => new ScenarioWorkflowBinding
            {
                WorkflowRef = b.WorkflowRef,
                DisplayName = b.DisplayName,
                Cron = b.Cron,
                Enabled = b.Enabled
            }).ToList();
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

        private static List<Objective> MapObjectives(List<ScenarioObjectiveImportDto> dtos)
        {
            var now = DateTime.UtcNow;
            return dtos.Select(o => new Objective
            {
                Name = o.Name,
                Description = o.Description,
                Type = string.IsNullOrEmpty(o.Type) ? "MET" : o.Type,
                Priority = o.Priority > 0 ? o.Priority : 1,
                SuccessCriteria = o.SuccessCriteria,
                Assigned = o.Assigned,
                SortOrder = o.SortOrder,
                CreatedAt = now,
                UpdatedAt = now
            }).ToList();
        }

        /// <summary>
        /// Entities and edges created with the scenario. Edges name their endpoints by the caller's
        /// local entity id, resolved here to the GUIDs assigned to the new entities. CreatedAt is
        /// stepped so the tables keep the order they were given in, which is all they can express.
        /// </summary>
        private static void MapGraph(CreateScenarioDto dto, Scenario scenario)
        {
            var now = DateTime.UtcNow;
            var byLocalId = new Dictionary<string, ScenarioEntity>();

            scenario.Entities = dto.Entities.Select((e, i) =>
            {
                var entity = new ScenarioEntity
                {
                    Name = e.Name,
                    EntityType = string.IsNullOrEmpty(e.Type) ? "Custom" : e.Type,
                    Description = e.Description,
                    Properties = string.IsNullOrEmpty(e.Properties) ? "{}" : e.Properties,
                    Confidence = e.Confidence,
                    Origin = string.IsNullOrEmpty(e.Origin) ? "Operator" : e.Origin,
                    ExternalId = e.ExternalId,
                    IsReviewed = e.IsReviewed,
                    CreatedAt = now.AddMilliseconds(i),
                    UpdatedAt = now.AddMilliseconds(i)
                };
                if (!string.IsNullOrEmpty(e.Id)) byLocalId[e.Id] = entity;
                return entity;
            }).ToList();

            if (dto.Edges == null) return;

            var edges = new List<ScenarioEdge>();
            foreach (var (e, i) in dto.Edges.Select((e, i) => (e, i)))
            {
                if (!byLocalId.TryGetValue(e.From ?? string.Empty, out var source) ||
                    !byLocalId.TryGetValue(e.To ?? string.Empty, out var target))
                {
                    _log.Warn($"Edge {e.From} -> {e.To} skipped: unknown entity");
                    continue;
                }

                edges.Add(new ScenarioEdge
                {
                    SourceEntityId = source.Id,
                    TargetEntityId = target.Id,
                    EdgeType = string.IsNullOrEmpty(e.Type) ? "Custom" : e.Type,
                    Label = e.Label,
                    Weight = e.Weight,
                    Confidence = e.Confidence,
                    Origin = string.IsNullOrEmpty(e.Origin) ? "Operator" : e.Origin,
                    Properties = string.IsNullOrEmpty(e.Properties) ? "{}" : e.Properties,
                    IsReviewed = e.IsReviewed,
                    CreatedAt = now.AddMilliseconds(i)
                });
            }
            scenario.Edges = edges;
        }

        /// <summary>
        /// Rewrites the caller's local objective ids — in objective parents and in the timeline
        /// events that refer to them — to the database ids assigned by the first save.
        /// </summary>
        private static void RemapObjectiveIds(List<ScenarioObjectiveImportDto> dtos, Scenario scenario)
        {
            var objectives = scenario.Objectives.ToList();
            var ids = new Dictionary<int, int>();
            for (var i = 0; i < objectives.Count; i++) ids[dtos[i].Id] = objectives[i].Id;

            for (var i = 0; i < objectives.Count; i++)
            {
                if (dtos[i].ParentId is int parent && ids.TryGetValue(parent, out var parentId))
                {
                    objectives[i].ParentId = parentId;
                }
            }

            foreach (var e in scenario.ScenarioTimeline?.ScenarioTimelineEvents ?? [])
            {
                if (string.IsNullOrEmpty(e.ObjectiveIds)) continue;
                var mapped = JsonSerializer.Deserialize<List<int>>(e.ObjectiveIds)
                    .Where(ids.ContainsKey).Select(id => ids[id]).ToList();
                e.ObjectiveIds = mapped.Count > 0 ? JsonSerializer.Serialize(mapped) : null;
            }
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
                    Status = e.Status,
                    ObjectiveIds = e.ObjectiveIds?.Count > 0 ? JsonSerializer.Serialize(e.ObjectiveIds) : null,
                    TriggerKind = Enum.TryParse<TriggerKind>(e.TriggerKind, true, out var tk) ? tk : TriggerKind.PointInTime,
                    Schedule = e.Schedule,
                    TriggerCondition = e.TriggerCondition,
                    ExecutionType = Enum.TryParse<ExecutionType>(e.ExecutionType, true, out var et) ? et : ExecutionType.Manual,
                    WorkflowId = e.WorkflowId
                }).ToList()
            };
        }
    }
}
