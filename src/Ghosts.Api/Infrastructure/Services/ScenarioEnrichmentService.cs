// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace Ghosts.Api.Infrastructure.Services
{
    public interface IScenarioEnrichmentService
    {
        Task ImportAttackDataAsync(string stixJsonPath, CancellationToken ct);
        Task<List<AttackTechnique>> SearchTechniquesAsync(string query, string tactic, CancellationToken ct);
        Task<List<AttackGroup>> SearchGroupsAsync(string query, CancellationToken ct);
        Task<AttackTechnique> GetTechniqueAsync(string techniqueId, CancellationToken ct);
        Task<AttackGroup> GetGroupAsync(string groupId, CancellationToken ct);
        Task<List<AttackTechniqueSummaryDto>> GetTechniquesForGroupAsync(string groupId, CancellationToken ct);
        Task<ScenarioEnrichment> ApplyTechniqueAsync(int scenarioId, ApplyAttackEnrichmentDto dto, CancellationToken ct);
        Task<ScenarioEnrichment> ApplyGroupAsync(int scenarioId, ApplyGroupEnrichmentDto dto, CancellationToken ct);
        Task<List<ScenarioEnrichment>> GetEnrichmentsAsync(int scenarioId, CancellationToken ct);
        Task DeleteEnrichmentAsync(int enrichmentId, CancellationToken ct);
    }

    public class ScenarioEnrichmentService(ApplicationDbContext context) : IScenarioEnrichmentService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        public async Task ImportAttackDataAsync(string stixJsonPath, CancellationToken ct)
        {
            _log.Info($"Starting ATT&CK data import from {stixJsonPath}");

            if (!File.Exists(stixJsonPath))
            {
                _log.Error($"STIX file not found: {stixJsonPath}");
                throw new FileNotFoundException("STIX file not found", stixJsonPath);
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(stixJsonPath, ct);
                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                if (!root.TryGetProperty("objects", out var objects))
                {
                    _log.Error("STIX file does not contain 'objects' array");
                    throw new InvalidOperationException("Invalid STIX format");
                }

                var techniques = new List<AttackTechnique>();
                var groups = new List<AttackGroup>();
                var groupTechniques = new List<AttackGroupTechnique>();

                // First pass: build ID lookup dictionaries
                var stixIdToTechniqueId = new Dictionary<string, string>();
                var stixIdToGroupId = new Dictionary<string, string>();

                foreach (var obj in objects.EnumerateArray())
                {
                    if (!obj.TryGetProperty("type", out var typeProperty))
                        continue;

                    var type = typeProperty.GetString();
                    var stixId = obj.GetProperty("id").GetString();

                    if (type == "attack-pattern")
                    {
                        var externalId = GetExternalId(obj, "mitre-attack");
                        if (!string.IsNullOrEmpty(externalId))
                        {
                            stixIdToTechniqueId[stixId] = externalId;
                        }
                    }
                    else if (type == "intrusion-set")
                    {
                        var externalId = GetExternalId(obj, "mitre-attack");
                        if (!string.IsNullOrEmpty(externalId))
                        {
                            stixIdToGroupId[stixId] = externalId;
                        }
                    }
                }

                // Second pass: parse entities
                foreach (var obj in objects.EnumerateArray())
                {
                    if (!obj.TryGetProperty("type", out var typeProperty))
                        continue;

                    var type = typeProperty.GetString();

                    if (type == "attack-pattern")
                    {
                        var technique = ParseTechnique(obj);
                        if (technique != null)
                        {
                            techniques.Add(technique);
                        }
                    }
                    else if (type == "intrusion-set")
                    {
                        var group = ParseGroup(obj);
                        if (group != null)
                        {
                            groups.Add(group);
                        }
                    }
                    else if (type == "relationship")
                    {
                        var relationship = ParseRelationship(obj, stixIdToGroupId, stixIdToTechniqueId);
                        if (relationship != null)
                        {
                            groupTechniques.Add(relationship);
                        }
                    }
                }

                // Clear and insert in transaction
                await using var transaction = await _context.Database.BeginTransactionAsync(ct);
                try
                {
                    _log.Info("Clearing existing ATT&CK data");
                    _context.AttackGroupTechniques.RemoveRange(_context.AttackGroupTechniques);
                    _context.AttackTechniques.RemoveRange(_context.AttackTechniques);
                    _context.AttackGroups.RemoveRange(_context.AttackGroups);
                    await _context.SaveChangesAsync(ct);

                    _log.Info($"Inserting {techniques.Count} techniques");
                    await _context.AttackTechniques.AddRangeAsync(techniques, ct);
                    await _context.SaveChangesAsync(ct);

                    _log.Info($"Inserting {groups.Count} groups");
                    await _context.AttackGroups.AddRangeAsync(groups, ct);
                    await _context.SaveChangesAsync(ct);

                    _log.Info($"Inserting {groupTechniques.Count} group-technique relationships");
                    await _context.AttackGroupTechniques.AddRangeAsync(groupTechniques, ct);
                    await _context.SaveChangesAsync(ct);

                    await transaction.CommitAsync(ct);

                    _log.Info($"ATT&CK data import completed: {techniques.Count} techniques, {groups.Count} groups, {groupTechniques.Count} relationships");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _log.Error(ex, "Failed to import ATT&CK data");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error importing ATT&CK data from {stixJsonPath}");
                throw;
            }
        }

        public async Task<List<AttackTechnique>> SearchTechniquesAsync(string query, string tactic, CancellationToken ct)
        {
            var queryable = _context.AttackTechniques.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchTerm = $"%{query}%";
                queryable = queryable.Where(t => EF.Functions.ILike(t.Name, searchTerm) || EF.Functions.ILike(t.Id, searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(tactic))
            {
                queryable = queryable.Where(t => t.Tactics.Contains(tactic));
            }

            return await queryable
                .OrderBy(t => t.Id)
                .Take(50)
                .ToListAsync(ct);
        }

        public async Task<List<AttackGroup>> SearchGroupsAsync(string query, CancellationToken ct)
        {
            var queryable = _context.AttackGroups.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var searchTerm = $"%{query}%";
                queryable = queryable.Where(g => EF.Functions.ILike(g.Name, searchTerm) || EF.Functions.ILike(g.Aliases, searchTerm));
            }

            return await queryable
                .OrderBy(g => g.Id)
                .Take(50)
                .ToListAsync(ct);
        }

        public async Task<AttackTechnique> GetTechniqueAsync(string techniqueId, CancellationToken ct)
        {
            var technique = await _context.AttackTechniques
                .Include(t => t.Subtechniques)
                .FirstOrDefaultAsync(t => t.Id == techniqueId, ct);

            if (technique == null)
            {
                _log.Warn($"Technique not found: {techniqueId}");
                throw new InvalidOperationException($"Technique not found: {techniqueId}");
            }

            return technique;
        }

        public async Task<AttackGroup> GetGroupAsync(string groupId, CancellationToken ct)
        {
            var group = await _context.AttackGroups
                .Include(g => g.TechniqueUsages)
                    .ThenInclude(tu => tu.Technique)
                .FirstOrDefaultAsync(g => g.Id == groupId, ct);

            if (group == null)
            {
                _log.Warn($"Group not found: {groupId}");
                throw new InvalidOperationException($"Group not found: {groupId}");
            }

            return group;
        }

        public async Task<List<AttackTechniqueSummaryDto>> GetTechniquesForGroupAsync(string groupId, CancellationToken ct)
        {
            var group = await _context.AttackGroups
                .Include(g => g.TechniqueUsages)
                    .ThenInclude(tu => tu.Technique)
                .FirstOrDefaultAsync(g => g.Id == groupId, ct);

            if (group == null)
            {
                _log.Warn($"Group not found: {groupId}");
                throw new InvalidOperationException($"Group not found: {groupId}");
            }

            return group.TechniqueUsages
                .Select(tu => new AttackTechniqueSummaryDto(
                    tu.Technique.Id,
                    tu.Technique.Name,
                    tu.Technique.Tactics,
                    tu.Technique.IsSubtechnique))
                .ToList();
        }

        public async Task<ScenarioEnrichment> ApplyTechniqueAsync(int scenarioId, ApplyAttackEnrichmentDto dto, CancellationToken ct)
        {
            var scenario = await _context.Scenarios.FindAsync(scenarioId);
            if (scenario == null)
            {
                _log.Error($"Scenario not found: {scenarioId}");
                throw new InvalidOperationException("Scenario not found");
            }

            var technique = await GetTechniqueAsync(dto.TechniqueId, ct);

            var techniqueData = new
            {
                technique.Id,
                technique.Name,
                technique.Description,
                technique.Tactics,
                technique.Platforms,
                technique.DataSources,
                technique.Detection,
                technique.Url,
                technique.IsSubtechnique,
                technique.ParentId
            };

            var enrichment = new ScenarioEnrichment
            {
                ScenarioId = scenarioId,
                EntityId = dto.EntityId,
                EnrichmentType = "AttackTechnique",
                ExternalId = technique.Id,
                Name = technique.Name,
                Description = technique.Description,
                Data = JsonSerializer.Serialize(techniqueData),
                Source = "MitreAttack",
                CreatedAt = DateTime.UtcNow
            };

            _context.ScenarioEnrichments.Add(enrichment);
            await _context.SaveChangesAsync(ct);

            _log.Info($"Applied technique {technique.Id} to scenario {scenarioId}");

            return enrichment;
        }

        public async Task<ScenarioEnrichment> ApplyGroupAsync(int scenarioId, ApplyGroupEnrichmentDto dto, CancellationToken ct)
        {
            var scenario = await _context.Scenarios.FindAsync(scenarioId);
            if (scenario == null)
            {
                _log.Error($"Scenario not found: {scenarioId}");
                throw new InvalidOperationException("Scenario not found");
            }

            var group = await GetGroupAsync(dto.GroupId, ct);

            var groupData = new
            {
                group.Id,
                group.Name,
                group.Aliases,
                group.Description,
                group.Url,
                Techniques = group.TechniqueUsages.Select(tu => new
                {
                    tu.Technique.Id,
                    tu.Technique.Name,
                    tu.Use
                }).ToList()
            };

            var enrichment = new ScenarioEnrichment
            {
                ScenarioId = scenarioId,
                EntityId = dto.EntityId,
                EnrichmentType = "AttackGroup",
                ExternalId = group.Id,
                Name = group.Name,
                Description = group.Description,
                Data = JsonSerializer.Serialize(groupData),
                Source = "MitreAttack",
                CreatedAt = DateTime.UtcNow
            };

            _context.ScenarioEnrichments.Add(enrichment);
            await _context.SaveChangesAsync(ct);

            _log.Info($"Applied group {group.Id} to scenario {scenarioId}");

            return enrichment;
        }

        public async Task<List<ScenarioEnrichment>> GetEnrichmentsAsync(int scenarioId, CancellationToken ct)
        {
            return await _context.ScenarioEnrichments
                .Where(e => e.ScenarioId == scenarioId)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task DeleteEnrichmentAsync(int enrichmentId, CancellationToken ct)
        {
            var enrichment = await _context.ScenarioEnrichments.FindAsync(enrichmentId);
            if (enrichment == null)
            {
                _log.Error($"Enrichment not found: {enrichmentId}");
                throw new InvalidOperationException("Enrichment not found");
            }

            _context.ScenarioEnrichments.Remove(enrichment);
            await _context.SaveChangesAsync(ct);

            _log.Info($"Deleted enrichment: {enrichmentId}");
        }

        // Helper methods for STIX parsing
        private static string GetExternalId(JsonElement obj, string sourceName)
        {
            if (!obj.TryGetProperty("external_references", out var refs))
                return null;

            foreach (var reference in refs.EnumerateArray())
            {
                if (reference.TryGetProperty("source_name", out var source) &&
                    source.GetString() == sourceName &&
                    reference.TryGetProperty("external_id", out var externalId))
                {
                    return externalId.GetString();
                }
            }

            return null;
        }

        private static string GetExternalUrl(JsonElement obj, string sourceName)
        {
            if (!obj.TryGetProperty("external_references", out var refs))
                return string.Empty;

            foreach (var reference in refs.EnumerateArray())
            {
                if (reference.TryGetProperty("source_name", out var source) &&
                    source.GetString() == sourceName &&
                    reference.TryGetProperty("url", out var url))
                {
                    return url.GetString();
                }
            }

            return string.Empty;
        }

        private static AttackTechnique ParseTechnique(JsonElement obj)
        {
            var id = GetExternalId(obj, "mitre-attack");
            if (string.IsNullOrEmpty(id))
                return null;

            var name = obj.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : string.Empty;
            var description = obj.TryGetProperty("description", out var descProperty) ? descProperty.GetString() : string.Empty;

            var tactics = string.Empty;
            if (obj.TryGetProperty("kill_chain_phases", out var phases))
            {
                var tacticList = new List<string>();
                foreach (var phase in phases.EnumerateArray())
                {
                    if (phase.TryGetProperty("phase_name", out var phaseName))
                    {
                        tacticList.Add(phaseName.GetString());
                    }
                }
                tactics = string.Join(",", tacticList);
            }

            var platforms = string.Empty;
            if (obj.TryGetProperty("x_mitre_platforms", out var platformsArray))
            {
                var platformList = new List<string>();
                foreach (var platform in platformsArray.EnumerateArray())
                {
                    platformList.Add(platform.GetString());
                }
                platforms = string.Join(",", platformList);
            }

            var dataSources = string.Empty;
            if (obj.TryGetProperty("x_mitre_data_sources", out var dataSourcesArray))
            {
                var dataSourceList = new List<string>();
                foreach (var ds in dataSourcesArray.EnumerateArray())
                {
                    dataSourceList.Add(ds.GetString());
                }
                dataSources = string.Join(",", dataSourceList);
            }

            var detection = string.Empty;
            if (obj.TryGetProperty("x_mitre_detection", out var detectionProperty))
            {
                detection = detectionProperty.GetString() ?? string.Empty;
            }

            var url = GetExternalUrl(obj, "mitre-attack");
            var isSubtechnique = id.Contains('.');
            var parentId = isSubtechnique ? id.Split('.')[0] : null;

            var version = obj.TryGetProperty("x_mitre_version", out var versionProperty) ? versionProperty.GetString() : string.Empty;

            return new AttackTechnique
            {
                Id = id,
                Name = name,
                Description = description,
                Tactics = tactics,
                Platforms = platforms,
                DataSources = dataSources,
                Detection = detection,
                Url = url,
                IsSubtechnique = isSubtechnique,
                ParentId = parentId,
                Version = version,
                ImportedAt = DateTime.UtcNow
            };
        }

        private static AttackGroup ParseGroup(JsonElement obj)
        {
            var id = GetExternalId(obj, "mitre-attack");
            if (string.IsNullOrEmpty(id))
                return null;

            var name = obj.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : string.Empty;
            var description = obj.TryGetProperty("description", out var descProperty) ? descProperty.GetString() : string.Empty;

            var aliases = string.Empty;
            if (obj.TryGetProperty("aliases", out var aliasesArray))
            {
                var aliasList = new List<string>();
                foreach (var alias in aliasesArray.EnumerateArray())
                {
                    aliasList.Add(alias.GetString());
                }
                aliases = string.Join(",", aliasList);
            }

            var url = GetExternalUrl(obj, "mitre-attack");
            var version = obj.TryGetProperty("x_mitre_version", out var versionProperty) ? versionProperty.GetString() : string.Empty;

            return new AttackGroup
            {
                Id = id,
                Name = name,
                Aliases = aliases,
                Description = description,
                Url = url,
                Version = version,
                ImportedAt = DateTime.UtcNow
            };
        }

        private static AttackGroupTechnique ParseRelationship(JsonElement obj,
            Dictionary<string, string> stixIdToGroupId,
            Dictionary<string, string> stixIdToTechniqueId)
        {
            if (!obj.TryGetProperty("relationship_type", out var relType) ||
                relType.GetString() != "uses")
            {
                return null;
            }

            var sourceRef = obj.GetProperty("source_ref").GetString();
            var targetRef = obj.GetProperty("target_ref").GetString();

            if (!stixIdToGroupId.TryGetValue(sourceRef, out var groupId) ||
                !stixIdToTechniqueId.TryGetValue(targetRef, out var techniqueId))
            {
                return null;
            }

            var use = obj.TryGetProperty("description", out var useProperty) ? useProperty.GetString() : string.Empty;

            return new AttackGroupTechnique
            {
                GroupId = groupId,
                TechniqueId = techniqueId,
                Use = use
            };
        }
    }
}
