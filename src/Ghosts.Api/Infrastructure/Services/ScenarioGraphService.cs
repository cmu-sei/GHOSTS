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
    public interface IScenarioGraphService
    {
        // Entities
        Task<List<ScenarioEntity>> GetEntitiesAsync(int scenarioId, string entityType, CancellationToken ct);
        Task<ScenarioEntity> GetEntityAsync(Guid entityId, CancellationToken ct);
        Task<ScenarioEntity> CreateEntityAsync(int scenarioId, CreateScenarioEntityDto dto, CancellationToken ct);
        Task<ScenarioEntity> UpdateEntityAsync(Guid entityId, UpdateScenarioEntityDto dto, CancellationToken ct);
        Task DeleteEntityAsync(Guid entityId, CancellationToken ct);
        Task<ScenarioEntity> MergeEntitiesAsync(Guid keepEntityId, Guid mergeEntityId, CancellationToken ct);

        // Edges
        Task<List<ScenarioEdge>> GetEdgesAsync(int scenarioId, string edgeType, CancellationToken ct);
        Task<ScenarioEdge> CreateEdgeAsync(int scenarioId, CreateScenarioEdgeDto dto, CancellationToken ct);
        Task DeleteEdgeAsync(Guid edgeId, CancellationToken ct);

        // Graph
        Task<ScenarioGraphDto> GetGraphAsync(int scenarioId, CancellationToken ct);
        Task<Dictionary<string, int>> GetGraphStatsAsync(int scenarioId, CancellationToken ct);
    }

    public class ScenarioGraphService(ApplicationDbContext context) : IScenarioGraphService
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private readonly ApplicationDbContext _context = context;

        // ──────────────────────────────────────────────
        // Entities
        // ──────────────────────────────────────────────

        public async Task<List<ScenarioEntity>> GetEntitiesAsync(int scenarioId, string entityType, CancellationToken ct)
        {
            var query = _context.ScenarioEntities
                .Where(e => e.ScenarioId == scenarioId);

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(e => e.EntityType == entityType);
            }

            return await query
                .Include(e => e.OutgoingEdges)
                .Include(e => e.IncomingEdges)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<ScenarioEntity> GetEntityAsync(Guid entityId, CancellationToken ct)
        {
            var entity = await _context.ScenarioEntities
                .Include(e => e.OutgoingEdges)
                .Include(e => e.IncomingEdges)
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
            {
                _log.Error($"ScenarioEntity not found: {entityId}");
                throw new InvalidOperationException("ScenarioEntity not found");
            }

            return entity;
        }

        public async Task<ScenarioEntity> CreateEntityAsync(int scenarioId, CreateScenarioEntityDto dto, CancellationToken ct)
        {
            var entity = new ScenarioEntity
            {
                Id = Guid.NewGuid(),
                ScenarioId = scenarioId,
                Name = dto.Name,
                EntityType = dto.EntityType,
                Description = dto.Description,
                Properties = dto.Properties ?? "{}",
                Confidence = dto.Confidence > 0 ? dto.Confidence : 1.0m,
                Origin = "Operator",
                IsReviewed = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ScenarioEntities.Add(entity);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create entity: {operation}");
                throw new InvalidOperationException("Could not create ScenarioEntity");
            }

            _log.Info($"Created entity: {entity.Id} - {entity.Name} ({entity.EntityType})");

            return entity;
        }

        public async Task<ScenarioEntity> UpdateEntityAsync(Guid entityId, UpdateScenarioEntityDto dto, CancellationToken ct)
        {
            var entity = await _context.ScenarioEntities
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
            {
                _log.Error($"ScenarioEntity not found: {entityId}");
                throw new InvalidOperationException("ScenarioEntity not found");
            }

            entity.Name = dto.Name;
            entity.EntityType = dto.EntityType;
            entity.Description = dto.Description;
            entity.Properties = dto.Properties ?? "{}";
            entity.Confidence = dto.Confidence;
            entity.IsReviewed = dto.IsReviewed;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);

            _log.Info($"Updated entity: {entity.Id} - {entity.Name}");

            return entity;
        }

        public async Task DeleteEntityAsync(Guid entityId, CancellationToken ct)
        {
            var entity = await _context.ScenarioEntities
                .Include(e => e.OutgoingEdges)
                .Include(e => e.IncomingEdges)
                .FirstOrDefaultAsync(e => e.Id == entityId, ct);

            if (entity == null)
            {
                _log.Error($"ScenarioEntity not found: {entityId}");
                throw new InvalidOperationException("ScenarioEntity not found");
            }

            // Remove all edges (cascades should handle this, but being explicit)
            _context.ScenarioEdges.RemoveRange(entity.OutgoingEdges);
            _context.ScenarioEdges.RemoveRange(entity.IncomingEdges);
            _context.ScenarioEntities.Remove(entity);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not delete entity: {operation}");
                throw new InvalidOperationException("Could not delete ScenarioEntity");
            }

            _log.Info($"Deleted entity: {entityId}");
        }

        public async Task<ScenarioEntity> MergeEntitiesAsync(Guid keepEntityId, Guid mergeEntityId, CancellationToken ct)
        {
            var keepEntity = await _context.ScenarioEntities
                .FirstOrDefaultAsync(e => e.Id == keepEntityId, ct);

            var mergeEntity = await _context.ScenarioEntities
                .Include(e => e.OutgoingEdges)
                .Include(e => e.IncomingEdges)
                .FirstOrDefaultAsync(e => e.Id == mergeEntityId, ct);

            if (keepEntity == null || mergeEntity == null)
            {
                _log.Error($"Entity not found for merge: keep={keepEntityId}, merge={mergeEntityId}");
                throw new InvalidOperationException("One or both entities not found for merge");
            }

            if (keepEntity.ScenarioId != mergeEntity.ScenarioId)
            {
                _log.Error($"Cannot merge entities from different scenarios");
                throw new InvalidOperationException("Cannot merge entities from different scenarios");
            }

            // Reassign all edges from mergeEntity to keepEntity
            foreach (var edge in mergeEntity.OutgoingEdges)
            {
                edge.SourceEntityId = keepEntityId;
            }

            foreach (var edge in mergeEntity.IncomingEdges)
            {
                edge.TargetEntityId = keepEntityId;
            }

            // Remove the merged entity
            _context.ScenarioEntities.Remove(mergeEntity);

            await _context.SaveChangesAsync(ct);

            _log.Info($"Merged entity {mergeEntityId} into {keepEntityId}");

            // Reload the keep entity with updated edges
            return await GetEntityAsync(keepEntityId, ct);
        }

        // ──────────────────────────────────────────────
        // Edges
        // ──────────────────────────────────────────────

        public async Task<List<ScenarioEdge>> GetEdgesAsync(int scenarioId, string edgeType, CancellationToken ct)
        {
            var query = _context.ScenarioEdges
                .Where(e => e.ScenarioId == scenarioId);

            if (!string.IsNullOrEmpty(edgeType))
            {
                query = query.Where(e => e.EdgeType == edgeType);
            }

            return await query
                .Include(e => e.SourceEntity)
                .Include(e => e.TargetEntity)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(ct);
        }

        public async Task<ScenarioEdge> CreateEdgeAsync(int scenarioId, CreateScenarioEdgeDto dto, CancellationToken ct)
        {
            // Validate that both entities exist and belong to the same scenario
            var sourceEntity = await _context.ScenarioEntities
                .FirstOrDefaultAsync(e => e.Id == dto.SourceEntityId && e.ScenarioId == scenarioId, ct);

            var targetEntity = await _context.ScenarioEntities
                .FirstOrDefaultAsync(e => e.Id == dto.TargetEntityId && e.ScenarioId == scenarioId, ct);

            if (sourceEntity == null || targetEntity == null)
            {
                _log.Error($"Source or target entity not found in scenario {scenarioId}");
                throw new InvalidOperationException("Source or target entity not found in this scenario");
            }

            var edge = new ScenarioEdge
            {
                Id = Guid.NewGuid(),
                ScenarioId = scenarioId,
                SourceEntityId = dto.SourceEntityId,
                TargetEntityId = dto.TargetEntityId,
                EdgeType = dto.EdgeType,
                Label = dto.Label ?? string.Empty,
                Weight = dto.Weight > 0 ? dto.Weight : 1.0m,
                Confidence = dto.Confidence > 0 ? dto.Confidence : 1.0m,
                Origin = "Operator",
                IsReviewed = false,
                Properties = "{}",
                CreatedAt = DateTime.UtcNow
            };

            _context.ScenarioEdges.Add(edge);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not create edge: {operation}");
                throw new InvalidOperationException("Could not create ScenarioEdge");
            }

            _log.Info($"Created edge: {edge.Id} - {edge.EdgeType} from {sourceEntity.Name} to {targetEntity.Name}");

            return edge;
        }

        public async Task DeleteEdgeAsync(Guid edgeId, CancellationToken ct)
        {
            var edge = await _context.ScenarioEdges.FindAsync(edgeId);
            if (edge == null)
            {
                _log.Error($"ScenarioEdge not found: {edgeId}");
                throw new InvalidOperationException("ScenarioEdge not found");
            }

            _context.ScenarioEdges.Remove(edge);

            var operation = await _context.SaveChangesAsync(ct);
            if (operation < 1)
            {
                _log.Error($"Could not delete edge: {operation}");
                throw new InvalidOperationException("Could not delete ScenarioEdge");
            }

            _log.Info($"Deleted edge: {edgeId}");
        }

        // ──────────────────────────────────────────────
        // Graph
        // ──────────────────────────────────────────────

        public async Task<ScenarioGraphDto> GetGraphAsync(int scenarioId, CancellationToken ct)
        {
            var entities = await _context.ScenarioEntities
                .Where(e => e.ScenarioId == scenarioId)
                .OrderBy(e => e.Name)
                .ToListAsync(ct);

            var edges = await _context.ScenarioEdges
                .Where(e => e.ScenarioId == scenarioId)
                .ToListAsync(ct);

            var entityDtos = entities.Select(e => new ScenarioEntityDto(
                e.Id,
                e.Name,
                e.EntityType,
                e.Description,
                e.Properties,
                e.Confidence,
                e.Origin,
                e.SourceId,
                e.NpcId,
                e.ExternalId,
                e.IsReviewed,
                e.CreatedAt
            )).ToList();

            var edgeDtos = edges.Select(e => new ScenarioEdgeDto(
                e.Id,
                e.SourceEntityId,
                e.TargetEntityId,
                e.EdgeType,
                e.Label,
                e.Weight,
                e.Confidence,
                e.Origin,
                e.IsReviewed
            )).ToList();

            return new ScenarioGraphDto(entityDtos, edgeDtos);
        }

        public async Task<Dictionary<string, int>> GetGraphStatsAsync(int scenarioId, CancellationToken ct)
        {
            var stats = await _context.ScenarioEntities
                .Where(e => e.ScenarioId == scenarioId)
                .GroupBy(e => e.EntityType)
                .Select(g => new { EntityType = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            return stats.ToDictionary(s => s.EntityType, s => s.Count);
        }
    }
}
