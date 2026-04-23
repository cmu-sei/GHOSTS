using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ghosts.Api.Infrastructure.Services;

public interface IExecutionMapService
{
    Task<List<MapLayerInfo>> GetLayersAsync(int executionId, CancellationToken ct);
    Task<GeoJsonFeatureCollection> GetFeaturesAsync(int executionId, string featureType, DateTime? timeFrom, DateTime? timeTo, string status, string team, CancellationToken ct);
    Task<GeoJsonFeatureCollection> GetConnectionsAsync(int executionId, CancellationToken ct);
    Task<MapTimelineInfo> GetTimelineAsync(int executionId, int bucketCount, CancellationToken ct);
    Task<List<MapSearchResult>> SearchAsync(int executionId, string query, CancellationToken ct);
    Task<MapEntityDetail> GetEntityDetailAsync(int executionId, string featureType, string entityId, CancellationToken ct);
    Task<GeoJsonFeatureCollection> GetAllFeaturesAsync(int executionId, DateTime? timeFrom, DateTime? timeTo, CancellationToken ct);
    Task<MapFeatureDto> CreateAsync(CreateMapFeatureDto dto, CancellationToken ct);
    Task<MapFeatureDto> UpdateAsync(int id, UpdateMapFeatureDto dto, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task<List<MapFeatureDto>> BulkCreateAsync(List<CreateMapFeatureDto> dtos, CancellationToken ct);
}

public class ExecutionMapService(ApplicationDbContext context, ILogger<ExecutionMapService> logger) : IExecutionMapService
{
    private static readonly string[] PointTypes = ["Machine", "Npc", "ScenarioEntity", "Site", "Event", "Poi"];
    private static readonly string[] ConnectionTypes = ["Connection", "Network"];

    public async Task<List<MapLayerInfo>> GetLayersAsync(int executionId, CancellationToken ct)
    {
        var execution = await context.Executions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution == null) return [];

        var scenarioId = execution.ScenarioId;

        var groups = await context.MapFeatures
            .AsNoTracking()
            .Where(f => f.ExecutionId == executionId || f.ScenarioId == scenarioId || (f.ExecutionId == null && f.ScenarioId == null))
            .GroupBy(f => f.FeatureType)
            .Select(g => new { FeatureType = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var layers = new List<MapLayerInfo>();
        var typeLabels = new Dictionary<string, (string label, bool defaultOn)>
        {
            ["Poi"] = ("Points of Interest", true),
            ["Npc"] = ("Agents / NPCs", true),
            ["Machine"] = ("Machines / Endpoints", true),
            ["Site"] = ("Sites / Facilities", true),
            ["Network"] = ("Networks / Links", true),
            ["Event"] = ("Events / Incidents", true),
            ["Connection"] = ("Connections", false),
            ["ScenarioEntity"] = ("Scenario Entities", true)
        };

        foreach (var g in groups)
        {
            var (label, defaultOn) = typeLabels.GetValueOrDefault(g.FeatureType, (g.FeatureType, true));
            layers.Add(new MapLayerInfo(
                g.FeatureType.ToLowerInvariant(),
                label,
                g.FeatureType,
                g.Count,
                defaultOn
            ));
        }

        return layers.OrderBy(l => l.Label).ToList();
    }

    public async Task<GeoJsonFeatureCollection> GetFeaturesAsync(
        int executionId, string featureType, DateTime? timeFrom, DateTime? timeTo,
        string status, string team, CancellationToken ct)
    {
        var execution = await context.Executions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution == null)
            return new GeoJsonFeatureCollection("FeatureCollection", []);

        var scenarioId = execution.ScenarioId;

        var query = context.MapFeatures.AsNoTracking()
            .Where(f => f.ExecutionId == executionId || f.ScenarioId == scenarioId || (f.ExecutionId == null && f.ScenarioId == null));

        if (!string.IsNullOrEmpty(featureType))
            query = query.Where(f => f.FeatureType == featureType);

        if (timeFrom.HasValue)
            query = query.Where(f => f.ValidFrom == null || f.ValidFrom <= timeTo || f.ValidTo == null || f.ValidTo >= timeFrom);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(f => f.Status == status);

        if (!string.IsNullOrEmpty(team))
            query = query.Where(f => f.Team == team);

        var features = await query.ToListAsync(ct);
        return ToGeoJson(features);
    }

    public async Task<GeoJsonFeatureCollection> GetConnectionsAsync(int executionId, CancellationToken ct)
    {
        var execution = await context.Executions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution == null)
            return new GeoJsonFeatureCollection("FeatureCollection", []);

        var scenarioId = execution.ScenarioId;

        var connections = await context.MapFeatures.AsNoTracking()
            .Where(f => (f.ExecutionId == executionId || f.ScenarioId == scenarioId)
                && ConnectionTypes.Contains(f.FeatureType)
                && f.SourceFeatureId != null && f.TargetFeatureId != null)
            .ToListAsync(ct);

        if (connections.Count == 0)
            return new GeoJsonFeatureCollection("FeatureCollection", []);

        // Build lookup of feature positions
        var allEntityIds = connections
            .SelectMany(c => new[] { c.SourceFeatureId!, c.TargetFeatureId! })
            .Distinct()
            .ToList();

        var endpoints = await context.MapFeatures.AsNoTracking()
            .Where(f => allEntityIds.Contains(f.EntityId))
            .ToDictionaryAsync(f => f.EntityId, f => f, ct);

        var geoFeatures = new List<GeoJsonFeature>();

        foreach (var conn in connections)
        {
            if (!endpoints.TryGetValue(conn.SourceFeatureId!, out var source) ||
                !endpoints.TryGetValue(conn.TargetFeatureId!, out var target))
                continue;

            var geometry = new GeoJsonGeometry("LineString", new[]
            {
                new[] { source.Longitude, source.Latitude },
                new[] { target.Longitude, target.Latitude }
            });

            var props = BuildProperties(conn);
            geoFeatures.Add(new GeoJsonFeature("Feature", geometry, props));
        }

        return new GeoJsonFeatureCollection("FeatureCollection", geoFeatures);
    }

    public async Task<MapTimelineInfo> GetTimelineAsync(int executionId, int bucketCount, CancellationToken ct)
    {
        var execution = await context.Executions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution == null)
            return new MapTimelineInfo(null, null, 0, []);

        var scenarioId = execution.ScenarioId;

        var events = await context.MapFeatures.AsNoTracking()
            .Where(f => (f.ExecutionId == executionId || f.ScenarioId == scenarioId)
                && f.FeatureType == "Event" && f.ValidFrom != null)
            .Select(f => f.ValidFrom!.Value)
            .ToListAsync(ct);

        if (events.Count == 0)
            return new MapTimelineInfo(null, null, 0, []);

        var earliest = events.Min();
        var latest = events.Max();

        if (bucketCount <= 0) bucketCount = 20;
        var span = latest - earliest;
        if (span.TotalSeconds < 1) span = TimeSpan.FromHours(1);
        var bucketSpan = TimeSpan.FromTicks(span.Ticks / bucketCount);

        var buckets = new List<MapTimelineBucket>();
        for (var i = 0; i < bucketCount; i++)
        {
            var start = earliest + TimeSpan.FromTicks(bucketSpan.Ticks * i);
            var end = i == bucketCount - 1 ? latest.AddSeconds(1) : earliest + TimeSpan.FromTicks(bucketSpan.Ticks * (i + 1));
            var count = events.Count(e => e >= start && e < end);
            buckets.Add(new MapTimelineBucket(start, end, count));
        }

        return new MapTimelineInfo(earliest, latest, events.Count, buckets);
    }

    public async Task<List<MapSearchResult>> SearchAsync(int executionId, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var execution = await context.Executions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution == null) return [];

        var scenarioId = execution.ScenarioId;
        var q = query.ToLowerInvariant();

        var matches = await context.MapFeatures.AsNoTracking()
            .Where(f => (f.ExecutionId == executionId || f.ScenarioId == scenarioId || (f.ExecutionId == null && f.ScenarioId == null))
                && !ConnectionTypes.Contains(f.FeatureType)
                && (f.Label.ToLower().Contains(q) || f.EntityId.ToLower().Contains(q)
                    || f.Category.ToLower().Contains(q) || f.Description.ToLower().Contains(q)))
            .Take(20)
            .ToListAsync(ct);

        return matches.Select(m =>
        {
            var score = m.Label.ToLowerInvariant().StartsWith(q) ? 1.0 :
                        m.Label.ToLowerInvariant().Contains(q) ? 0.8 : 0.5;
            return new MapSearchResult(m.EntityId, m.FeatureType, m.Label, m.Category, m.Latitude, m.Longitude, score);
        })
        .OrderByDescending(r => r.Score)
        .ToList();
    }

    public async Task<MapEntityDetail> GetEntityDetailAsync(int executionId, string featureType, string entityId, CancellationToken ct)
    {
        var feature = await context.MapFeatures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.EntityId == entityId && f.FeatureType == featureType, ct);

        if (feature == null) return null;

        // Find related entities via connections
        var related = await context.MapFeatures.AsNoTracking()
            .Where(f => ConnectionTypes.Contains(f.FeatureType)
                && (f.SourceFeatureId == entityId || f.TargetFeatureId == entityId))
            .ToListAsync(ct);

        var relatedEntityIds = related
            .SelectMany(r => new[] { r.SourceFeatureId, r.TargetFeatureId })
            .Where(id => id != null && id != entityId)
            .Distinct()
            .ToList();

        var relatedFeatures = relatedEntityIds.Count > 0
            ? await context.MapFeatures.AsNoTracking()
                .Where(f => relatedEntityIds.Contains(f.EntityId) && !ConnectionTypes.Contains(f.FeatureType))
                .ToListAsync(ct)
            : [];

        var relatedEntities = relatedFeatures.Select(rf =>
        {
            var conn = related.FirstOrDefault(r => r.SourceFeatureId == rf.EntityId || r.TargetFeatureId == rf.EntityId);
            return new MapRelatedEntity(rf.EntityId, rf.FeatureType, rf.Label, conn?.Category ?? "Connected");
        }).ToList();

        // Get recent events near this entity
        var recentEvents = await context.MapFeatures.AsNoTracking()
            .Where(f => f.FeatureType == "Event"
                && (f.ExecutionId == feature.ExecutionId || f.ScenarioId == feature.ScenarioId)
                && f.ValidFrom != null)
            .OrderByDescending(f => f.ValidFrom)
            .Take(10)
            .ToListAsync(ct);

        // Filter events that reference this entity in properties
        var entityEvents = recentEvents
            .Where(e => e.Properties.Contains(entityId) || e.SourceFeatureId == entityId || e.TargetFeatureId == entityId
                || IsNearby(e, feature, 0.01))
            .Select(e => new MapRecentEvent(e.Id, e.ValidFrom ?? e.CreatedAt, e.Category, e.Description, e.Status))
            .Take(5)
            .ToList();

        var props = ParseProperties(feature.Properties);

        return new MapEntityDetail(
            feature.EntityId, feature.FeatureType, feature.Label, feature.Description,
            feature.Status, feature.Category, feature.Team,
            feature.Latitude, feature.Longitude, props,
            relatedEntities, entityEvents
        );
    }

    public async Task<GeoJsonFeatureCollection> GetAllFeaturesAsync(int executionId, DateTime? timeFrom, DateTime? timeTo, CancellationToken ct)
    {
        var execution = await context.Executions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == executionId, ct);

        if (execution == null)
            return new GeoJsonFeatureCollection("FeatureCollection", []);

        var scenarioId = execution.ScenarioId;

        var query = context.MapFeatures.AsNoTracking()
            .Where(f => f.ExecutionId == executionId || f.ScenarioId == scenarioId || (f.ExecutionId == null && f.ScenarioId == null));

        if (timeFrom.HasValue && timeTo.HasValue)
        {
            query = query.Where(f =>
                f.ValidFrom == null || f.ValidTo == null ||
                (f.ValidFrom <= timeTo && f.ValidTo >= timeFrom));
        }

        var features = await query.ToListAsync(ct);
        return ToGeoJson(features);
    }

    // ── CRUD ──

    public async Task<MapFeatureDto> CreateAsync(CreateMapFeatureDto dto, CancellationToken ct)
    {
        var feature = new MapFeature
        {
            FeatureType = dto.FeatureType,
            EntityId = dto.EntityId,
            ScenarioId = dto.ScenarioId,
            ExecutionId = dto.ExecutionId,
            Label = dto.Label,
            Description = dto.Description ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Geometry = dto.Geometry,
            Status = dto.Status ?? "Active",
            Category = dto.Category ?? string.Empty,
            Team = dto.Team ?? string.Empty,
            Properties = dto.Properties ?? "{}",
            SourceFeatureId = dto.SourceFeatureId,
            TargetFeatureId = dto.TargetFeatureId,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            CreatedAt = DateTime.UtcNow
        };

        context.MapFeatures.Add(feature);
        await context.SaveChangesAsync(ct);
        return ToDto(feature);
    }

    public async Task<MapFeatureDto> UpdateAsync(int id, UpdateMapFeatureDto dto, CancellationToken ct)
    {
        var feature = await context.MapFeatures.FindAsync([id], ct);
        if (feature == null) return null;

        if (dto.Label != null) feature.Label = dto.Label;
        if (dto.Description != null) feature.Description = dto.Description;
        if (dto.Latitude.HasValue) feature.Latitude = dto.Latitude.Value;
        if (dto.Longitude.HasValue) feature.Longitude = dto.Longitude.Value;
        if (dto.Geometry != null) feature.Geometry = dto.Geometry;
        if (dto.Status != null) feature.Status = dto.Status;
        if (dto.Category != null) feature.Category = dto.Category;
        if (dto.Team != null) feature.Team = dto.Team;
        if (dto.Properties != null) feature.Properties = dto.Properties;
        if (dto.SourceFeatureId != null) feature.SourceFeatureId = dto.SourceFeatureId;
        if (dto.TargetFeatureId != null) feature.TargetFeatureId = dto.TargetFeatureId;
        if (dto.ValidFrom.HasValue) feature.ValidFrom = dto.ValidFrom;
        if (dto.ValidTo.HasValue) feature.ValidTo = dto.ValidTo;
        feature.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
        return ToDto(feature);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var feature = await context.MapFeatures.FindAsync([id], ct);
        if (feature == null) return false;

        context.MapFeatures.Remove(feature);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<MapFeatureDto>> BulkCreateAsync(List<CreateMapFeatureDto> dtos, CancellationToken ct)
    {
        var features = dtos.Select(dto => new MapFeature
        {
            FeatureType = dto.FeatureType,
            EntityId = dto.EntityId,
            ScenarioId = dto.ScenarioId,
            ExecutionId = dto.ExecutionId,
            Label = dto.Label,
            Description = dto.Description ?? string.Empty,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Geometry = dto.Geometry,
            Status = dto.Status ?? "Active",
            Category = dto.Category ?? string.Empty,
            Team = dto.Team ?? string.Empty,
            Properties = dto.Properties ?? "{}",
            SourceFeatureId = dto.SourceFeatureId,
            TargetFeatureId = dto.TargetFeatureId,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        context.MapFeatures.AddRange(features);
        await context.SaveChangesAsync(ct);
        return features.Select(ToDto).ToList();
    }

    private static MapFeatureDto ToDto(MapFeature f) => new(
        f.Id, f.FeatureType, f.EntityId, f.ScenarioId, f.ExecutionId,
        f.Label, f.Description, f.Latitude, f.Longitude, f.Geometry,
        f.Status, f.Category, f.Team, f.Properties,
        f.SourceFeatureId, f.TargetFeatureId,
        f.CreatedAt, f.UpdatedAt, f.ValidFrom, f.ValidTo
    );

    // ── Private helpers ──

    private static GeoJsonFeatureCollection ToGeoJson(List<MapFeature> features)
    {
        var geoFeatures = new List<GeoJsonFeature>();

        foreach (var f in features)
        {
            GeoJsonGeometry geometry;

            if (!string.IsNullOrEmpty(f.Geometry) && f.Geometry != "{}")
            {
                try
                {
                    var geo = JsonSerializer.Deserialize<JsonElement>(f.Geometry);
                    var type = geo.GetProperty("type").GetString() ?? "Point";
                    var coords = geo.GetProperty("coordinates");
                    geometry = new GeoJsonGeometry(type, JsonSerializer.Deserialize<object>(coords.GetRawText())!);
                }
                catch
                {
                    geometry = new GeoJsonGeometry("Point", new[] { f.Longitude, f.Latitude });
                }
            }
            else if (f.SourceFeatureId != null && f.TargetFeatureId != null)
            {
                continue; // connections handled separately
            }
            else
            {
                geometry = new GeoJsonGeometry("Point", new[] { f.Longitude, f.Latitude });
            }

            geoFeatures.Add(new GeoJsonFeature("Feature", geometry, BuildProperties(f)));
        }

        return new GeoJsonFeatureCollection("FeatureCollection", geoFeatures);
    }

    private static GeoJsonProperties BuildProperties(MapFeature f)
    {
        Dictionary<string, object> extra = null;
        if (f.Properties != "{}" && !string.IsNullOrEmpty(f.Properties))
        {
            try { extra = JsonSerializer.Deserialize<Dictionary<string, object>>(f.Properties); }
            catch { /* ignore parse errors */ }
        }

        return new GeoJsonProperties(
            $"{f.FeatureType}_{f.EntityId}",
            f.FeatureType,
            f.EntityId,
            f.Label,
            f.Description,
            f.Status,
            f.Category,
            f.Team,
            f.ScenarioId,
            f.ExecutionId,
            f.CreatedAt,
            f.ValidFrom,
            f.ValidTo,
            extra
        );
    }

    private static Dictionary<string, object> ParseProperties(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }

    private static bool IsNearby(MapFeature a, MapFeature b, double threshold)
    {
        return Math.Abs(a.Latitude - b.Latitude) < threshold && Math.Abs(a.Longitude - b.Longitude) < threshold;
    }
}
