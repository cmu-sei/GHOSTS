// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Animator;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioCompilerService
    {
        Task<ScenarioCompilation> CompileAsync(int scenarioId, CompileScenarioDto dto, CancellationToken ct);
        Task<ScenarioCompilation> GetCompilationAsync(int compilationId, CancellationToken ct);
        Task<List<ScenarioCompilation>> GetCompilationsAsync(int scenarioId, CancellationToken ct);
        Task DeleteCompilationAsync(int compilationId, CancellationToken ct);
    }

    public class ScenarioCompilerService(ApplicationDbContext context) : IScenarioCompilerService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task<ScenarioCompilation> CompileAsync(int scenarioId, CompileScenarioDto dto, CancellationToken ct)
        {
            _log.Info($"=== CompileAsync ENTERED: ScenarioId={scenarioId}, Name={dto.Name} ===");

            _log.Info("Querying database for scenario with includes");
            var scenario = await _context.Scenarios
                .Include(s => s.Entities)
                .Include(s => s.Edges)
                .Include(s => s.Enrichments)
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
                .Include(s => s.ScenarioTimeline)
                    .ThenInclude(t => t.ScenarioTimelineEvents)
                .FirstOrDefaultAsync(s => s.Id == scenarioId, ct);

            if (scenario == null)
            {
                _log.Error($"Scenario not found: {scenarioId}");
                throw new InvalidOperationException("Scenario not found");
            }

            var compilation = new ScenarioCompilation
            {
                ScenarioId = scenarioId,
                Name = dto.Name,
                Status = "Compiling",
                CreatedAt = DateTime.UtcNow
            };

            _context.ScenarioCompilations.Add(compilation);
            await _context.SaveChangesAsync(ct);

            try
            {
                _log.Info($"Scenario loaded - Has Parameters: {scenario.ScenarioParameters != null}, Has TechEnv: {scenario.TechnicalEnvironment != null}, Has Timeline: {scenario.ScenarioTimeline != null}");
                _log.Info($"Entity count: {scenario.Entities?.Count ?? 0}, Edge count: {scenario.Edges?.Count ?? 0}");

                // Initialize scenario components if they don't exist
                if (scenario.ScenarioParameters == null)
                {
                    _log.Info("Creating new ScenarioParameters");
                    scenario.ScenarioParameters = new ScenarioParameters
                    {
                        ScenarioId = scenarioId
                    };
                    _context.ScenarioParameters.Add(scenario.ScenarioParameters);
                    await _context.SaveChangesAsync(ct);
                    _log.Info($"ScenarioParameters created with ID: {scenario.ScenarioParameters.Id}");
                }

                if (scenario.TechnicalEnvironment == null)
                {
                    _log.Info("Creating new TechnicalEnvironment");
                    scenario.TechnicalEnvironment = new TechnicalEnvironment
                    {
                        ScenarioId = scenarioId
                    };
                    _context.TechnicalEnvironments.Add(scenario.TechnicalEnvironment);
                    await _context.SaveChangesAsync(ct);
                    _log.Info($"TechnicalEnvironment created with ID: {scenario.TechnicalEnvironment.Id}");
                }

                if (scenario.ScenarioTimeline == null)
                {
                    _log.Info("Creating new ScenarioTimeline");
                    scenario.ScenarioTimeline = new ScenarioTimeline
                    {
                        ScenarioId = scenarioId
                    };
                    _context.ScenarioTimelines.Add(scenario.ScenarioTimeline);
                    await _context.SaveChangesAsync(ct);
                    _log.Info($"ScenarioTimeline created with ID: {scenario.ScenarioTimeline.Id}");
                }

                var npcCount = 0;
                var timelineEventCount = 0;
                var injectCount = 0;

                // Map entities to scenario model
                await MapEntitiesToScenarioModel(scenario, dto.GenerateNpcs, ct);

                // Generate NPCs if requested
                if (dto.GenerateNpcs)
                {
                    npcCount = await GenerateNpcsAsync(scenario, ct);
                }

                // Generate timeline events if requested
                if (dto.GenerateTimeline || dto.MapAttackToInjects)
                {
                    timelineEventCount = await GenerateTimelineEventsAsync(scenario, ct);
                }

                // Map attack techniques to injects if requested
                if (dto.MapAttackToInjects)
                {
                    injectCount = await MapAttackToInjectsAsync(scenario, ct);
                }

                // Build compilation package (exclude navigation properties to avoid circular references)
                var packageData = new
                {
                    Scenario = new
                    {
                        scenario.Id,
                        scenario.Name,
                        scenario.Description
                    },
                    Parameters = new
                    {
                        scenario.ScenarioParameters.Id,
                        scenario.ScenarioParameters.Objectives,
                        scenario.ScenarioParameters.PoliticalContext,
                        scenario.ScenarioParameters.RulesOfEngagement,
                        scenario.ScenarioParameters.VictoryConditions,
                        Nations = scenario.ScenarioParameters.Nations.Select(n => new { n.Name, n.Alignment }).ToList(),
                        ThreatActors = scenario.ScenarioParameters.ThreatActors.Select(ta => new { ta.Name, ta.Type, ta.Capability, ta.Ttps }).ToList(),
                        Injects = scenario.ScenarioParameters.Injects.Select(i => new { i.Trigger, i.Title }).ToList(),
                        UserPools = scenario.ScenarioParameters.UserPools.Select(up => new { up.Role, up.Count }).ToList()
                    },
                    TechnicalEnvironment = new
                    {
                        scenario.TechnicalEnvironment.Id,
                        scenario.TechnicalEnvironment.NetworkTopology,
                        scenario.TechnicalEnvironment.Services,
                        scenario.TechnicalEnvironment.Assets,
                        scenario.TechnicalEnvironment.Defenses,
                        Vulnerabilities = scenario.TechnicalEnvironment.Vulnerabilities.Select(v => new { v.Asset, v.Cve, v.Severity }).ToList()
                    },
                    Timeline = new
                    {
                        scenario.ScenarioTimeline.Id,
                        scenario.ScenarioTimeline.ExerciseDuration,
                        Events = scenario.ScenarioTimeline.ScenarioTimelineEvents.Select(e => new { e.Number, e.Time, e.Assigned, e.Description, e.Status }).ToList()
                    },
                    Entities = scenario.Entities.Select(e => new
                    {
                        e.Id,
                        e.Name,
                        e.EntityType,
                        e.Description,
                        Properties = string.IsNullOrWhiteSpace(e.Properties) ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(e.Properties),
                        e.NpcId
                    }).ToList(),
                    Edges = scenario.Edges.Select(edge => new
                    {
                        edge.Id,
                        edge.SourceEntityId,
                        edge.TargetEntityId,
                        edge.EdgeType,
                        edge.Label
                    }).ToList(),
                    Enrichments = scenario.Enrichments.Select(enr => new
                    {
                        enr.Id,
                        enr.EnrichmentType,
                        enr.ExternalId,
                        enr.Name,
                        enr.Description
                    }).ToList(),
                    CompiledAt = DateTime.UtcNow
                };

                compilation.PackageData = JsonSerializer.Serialize(packageData);
                compilation.NpcCount = npcCount;
                compilation.TimelineEventCount = timelineEventCount;
                compilation.InjectCount = injectCount;
                compilation.Status = "Completed";
                compilation.CompletedAt = DateTime.UtcNow;

                scenario.BuilderStatus = "Compiled";

                await _context.SaveChangesAsync(ct);

                _log.Info($"Compilation completed for scenario {scenarioId}: {npcCount} NPCs, {timelineEventCount} timeline events, {injectCount} injects");

                return compilation;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Compilation failed for scenario {scenarioId}");

                compilation.Status = "Failed";
                compilation.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync(ct);

                throw;
            }
        }

        public async Task<ScenarioCompilation> GetCompilationAsync(int compilationId, CancellationToken ct)
        {
            var compilation = await _context.ScenarioCompilations
                .FirstOrDefaultAsync(c => c.Id == compilationId, ct);

            if (compilation == null)
            {
                _log.Error($"Compilation not found: {compilationId}");
                throw new InvalidOperationException("Compilation not found");
            }

            return compilation;
        }

        public async Task<List<ScenarioCompilation>> GetCompilationsAsync(int scenarioId, CancellationToken ct)
        {
            return await _context.ScenarioCompilations
                .Where(c => c.ScenarioId == scenarioId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task DeleteCompilationAsync(int compilationId, CancellationToken ct)
        {
            var compilation = await _context.ScenarioCompilations.FindAsync(compilationId);
            if (compilation == null)
            {
                _log.Error($"Compilation not found: {compilationId}");
                throw new InvalidOperationException("Compilation not found");
            }

            _context.ScenarioCompilations.Remove(compilation);
            await _context.SaveChangesAsync(ct);

            _log.Info($"Deleted compilation: {compilationId}");
        }

        // Helper methods

        private async Task MapEntitiesToScenarioModel(Scenario scenario, bool linkNpcs, CancellationToken ct)
        {
            _log.Info($"Mapping entities to scenario model for scenario {scenario.Id}");

            // Map ThreatActor entities
            var threatActorEntities = scenario.Entities
                .Where(e => e.EntityType == "ThreatActor")
                .ToList();

            foreach (var entity in threatActorEntities)
            {
                var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Properties ?? "{}");
                var type = properties.ContainsKey("type") ? properties["type"].GetString() : "state";
                var capability = properties.ContainsKey("capability") && properties["capability"].ValueKind == JsonValueKind.Number
                    ? properties["capability"].GetInt32()
                    : 3;

                // Get TTPs from enrichments
                var ttps = scenario.Enrichments
                    .Where(e => e.EntityId == entity.Id && e.EnrichmentType == "AttackTechnique")
                    .Select(e => e.ExternalId)
                    .ToList();

                var existingThreatActor = scenario.ScenarioParameters.ThreatActors
                    .FirstOrDefault(ta => ta.Name.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));

                if (existingThreatActor == null)
                {
                    scenario.ScenarioParameters.ThreatActors.Add(new ThreatActor
                    {
                        Name = entity.Name,
                        Type = type,
                        Capability = capability,
                        Ttps = string.Join(",", ttps)
                    });
                }
            }

            // Map Location entities to Nations
            var locationEntities = scenario.Entities
                .Where(e => e.EntityType == "Location")
                .ToList();

            foreach (var entity in locationEntities)
            {
                var existingNation = scenario.ScenarioParameters.Nations
                    .FirstOrDefault(n => n.Name.Equals(entity.Name, StringComparison.OrdinalIgnoreCase));

                if (existingNation == null)
                {
                    scenario.ScenarioParameters.Nations.Add(new Nation
                    {
                        Name = entity.Name,
                        Alignment = "neutral"
                    });
                }
            }

            // Map Person entities to UserPools
            var personEntities = scenario.Entities
                .Where(e => e.EntityType == "Person")
                .ToList();

            var roleGroups = personEntities
                .GroupBy(e =>
                {
                    var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(e.Properties ?? "{}");
                    return properties.ContainsKey("role") ? properties["role"].GetString() : "User";
                })
                .ToList();

            foreach (var roleGroup in roleGroups)
            {
                var role = roleGroup.Key;
                var count = roleGroup.Count();

                var existingUserPool = scenario.ScenarioParameters.UserPools
                    .FirstOrDefault(up => up.Role.Equals(role, StringComparison.OrdinalIgnoreCase));

                if (existingUserPool == null)
                {
                    scenario.ScenarioParameters.UserPools.Add(new UserPool
                    {
                        Role = role,
                        Count = count
                    });
                }
            }

            // Map technical entities to TechnicalEnvironment
            var systemEntities = scenario.Entities
                .Where(e => e.EntityType == "System" || e.EntityType == "Network" || e.EntityType == "Software")
                .ToList();

            if (systemEntities.Any())
            {
                var assets = string.Join(", ", systemEntities.Where(e => e.EntityType == "System").Select(e => e.Name));
                var services = string.Join(", ", systemEntities.Where(e => e.EntityType == "Software").Select(e => e.Name));
                var networkTopology = string.Join(", ", systemEntities.Where(e => e.EntityType == "Network").Select(e => e.Name));

                if (!string.IsNullOrEmpty(assets))
                    scenario.TechnicalEnvironment.Assets = assets;
                if (!string.IsNullOrEmpty(services))
                    scenario.TechnicalEnvironment.Services = services;
                if (!string.IsNullOrEmpty(networkTopology))
                    scenario.TechnicalEnvironment.NetworkTopology = networkTopology;
            }

            // Map Vulnerability entities
            var vulnerabilityEntities = scenario.Entities
                .Where(e => e.EntityType == "Vulnerability")
                .ToList();

            foreach (var entity in vulnerabilityEntities)
            {
                var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entity.Properties ?? "{}");
                var cve = entity.ExternalId ?? (properties.ContainsKey("cve") ? properties["cve"].GetString() : "");
                var severity = properties.ContainsKey("severity") ? properties["severity"].GetString() : "medium";
                var asset = properties.ContainsKey("asset") ? properties["asset"].GetString() : "Unknown";

                scenario.TechnicalEnvironment.Vulnerabilities.Add(new Vulnerability
                {
                    Asset = asset,
                    Cve = cve,
                    Severity = severity
                });
            }

            await _context.SaveChangesAsync(ct);
        }

        private async Task<int> GenerateNpcsAsync(Scenario scenario, CancellationToken ct)
        {
            _log.Info($"Generating NPCs for scenario {scenario.Id}");

            var personEntities = scenario.Entities
                .Where(e => e.EntityType == "Person" && e.NpcId == null)
                .ToList();

            var npcCount = 0;

            foreach (var entity in personEntities)
            {
                try
                {
                    // Generate NPC profile using Animator
                    var npcProfile = Npc.Generate();

                    // Override name with entity name
                    npcProfile.Name = new Animator.Models.NameProfile { First = entity.Name.Split(' ')[0] };
                    if (entity.Name.Contains(' '))
                    {
                        npcProfile.Name.Last = entity.Name.Substring(entity.Name.IndexOf(' ') + 1);
                    }

                    // Create NPC record
                    var npcRecord = new NpcRecord
                    {
                        NpcProfile = npcProfile,
                        ScenarioId = scenario.Id,
                        CreatedUtc = DateTime.UtcNow
                    };

                    _context.Npcs.Add(npcRecord);
                    await _context.SaveChangesAsync(ct);

                    // Link entity to NPC
                    entity.NpcId = npcRecord.Id;
                    npcCount++;

                    _log.Debug($"Generated NPC {npcRecord.Id} for entity {entity.Name}");
                }
                catch (Exception ex)
                {
                    _log.Warn(ex, $"Failed to generate NPC for entity {entity.Name}");
                }
            }

            await _context.SaveChangesAsync(ct);

            _log.Info($"Generated {npcCount} NPCs for scenario {scenario.Id}");
            return npcCount;
        }

        private async Task<int> GenerateTimelineEventsAsync(Scenario scenario, CancellationToken ct)
        {
            _log.Info($"Generating timeline events for scenario {scenario.Id}");

            var enrichments = scenario.Enrichments
                .Where(e => e.EnrichmentType == "AttackTechnique")
                .OrderBy(e => e.CreatedAt)
                .ToList();

            var eventNumber = scenario.ScenarioTimeline.ScenarioTimelineEvents.Any()
                ? scenario.ScenarioTimeline.ScenarioTimelineEvents.Max(e => e.Number) + 1
                : 1;

            var minutesOffset = 0;
            var eventCount = 0;

            foreach (var enrichment in enrichments)
            {
                try
                {
                    var techniqueData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(enrichment.Data ?? "{}");
                    var tactics = techniqueData.ContainsKey("Tactics") ? techniqueData["Tactics"].GetString() : "";

                    var timelineEvent = new ScenarioTimelineEvent
                    {
                        Number = eventNumber++,
                        Time = $"T+{minutesOffset}m",
                        Assigned = "Red Team",
                        Description = $"{enrichment.ExternalId}: {enrichment.Name} - {enrichment.Description}",
                        Status = "Pending"
                    };

                    scenario.ScenarioTimeline.ScenarioTimelineEvents.Add(timelineEvent);
                    eventCount++;
                    minutesOffset += 15; // 15 minute intervals
                }
                catch (Exception ex)
                {
                    _log.Warn(ex, $"Failed to create timeline event for enrichment {enrichment.Id}");
                }
            }

            await _context.SaveChangesAsync(ct);

            _log.Info($"Generated {eventCount} timeline events for scenario {scenario.Id}");
            return eventCount;
        }

        private async Task<int> MapAttackToInjectsAsync(Scenario scenario, CancellationToken ct)
        {
            _log.Info($"Mapping ATT&CK techniques to injects for scenario {scenario.Id}");

            var enrichments = scenario.Enrichments
                .Where(e => e.EnrichmentType == "AttackTechnique")
                .OrderBy(e => e.CreatedAt)
                .ToList();

            var injectCount = 0;
            var minutesOffset = 0;

            foreach (var enrichment in enrichments)
            {
                try
                {
                    var inject = new Inject
                    {
                        Trigger = $"T+{minutesOffset}m",
                        Title = $"{enrichment.ExternalId}: {enrichment.Name}"
                    };

                    scenario.ScenarioParameters.Injects.Add(inject);
                    injectCount++;
                    minutesOffset += 15; // 15 minute intervals
                }
                catch (Exception ex)
                {
                    _log.Warn(ex, $"Failed to create inject for enrichment {enrichment.Id}");
                }
            }

            await _context.SaveChangesAsync(ct);

            _log.Info($"Mapped {injectCount} ATT&CK techniques to injects for scenario {scenario.Id}");
            return injectCount;
        }
    }
}
