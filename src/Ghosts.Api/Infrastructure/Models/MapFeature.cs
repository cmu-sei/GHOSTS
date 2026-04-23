#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

/// <summary>
/// Stores optional spatial metadata for any entity in the system.
/// Enables map visualization without requiring geographic data on core domain models.
/// </summary>
[Table("map_features")]
public class MapFeature
{
    public int Id { get; set; }

    /// <summary>
    /// The kind of domain entity this feature represents.
    /// Machine, Npc, ScenarioEntity, Site, Network, Event, Poi, Connection
    /// </summary>
    [MaxLength(50)]
    public string FeatureType { get; set; } = string.Empty;

    /// <summary>
    /// The primary key of the referenced entity, stored as string for polymorphic references.
    /// For Machines: Guid.ToString(), for Executions/Scenarios: int.ToString(), etc.
    /// </summary>
    [MaxLength(200)]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>
    /// Optional scenario scope. Null means global (e.g., a Machine's home location).
    /// </summary>
    public int? ScenarioId { get; set; }

    /// <summary>
    /// Optional execution scope. Null means the feature applies to all executions of the scenario.
    /// </summary>
    public int? ExecutionId { get; set; }

    [MaxLength(500)]
    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Point latitude (WGS-84)</summary>
    public double Latitude { get; set; }

    /// <summary>Point longitude (WGS-84)</summary>
    public double Longitude { get; set; }

    /// <summary>
    /// Optional GeoJSON geometry for lines, polygons, or multi-points.
    /// When null, a Point is constructed from Latitude/Longitude.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Geometry { get; set; }

    /// <summary>
    /// Visual/operational status: Online, Offline, Compromised, Degraded, Active, Inactive, Alert
    /// </summary>
    [MaxLength(50)]
    public string Status { get; set; } = "Active";

    /// <summary>Sub-classification within the FeatureType (e.g., "Server", "Workstation", "Router")</summary>
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>Team, faction, or organizational grouping</summary>
    [MaxLength(100)]
    public string Team { get; set; } = string.Empty;

    /// <summary>Arbitrary key-value metadata for the feature</summary>
    [Column(TypeName = "jsonb")]
    public string Properties { get; set; } = "{}";

    /// <summary>For connections: the EntityId of the source feature</summary>
    [MaxLength(200)]
    public string? SourceFeatureId { get; set; }

    /// <summary>For connections: the EntityId of the target feature</summary>
    [MaxLength(200)]
    public string? TargetFeatureId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Start of validity window for time-based replay</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>End of validity window for time-based replay</summary>
    public DateTime? ValidTo { get; set; }
}

// ── GeoJSON response types ──

public record GeoJsonFeature(
    string Type,
    GeoJsonGeometry Geometry,
    GeoJsonProperties Properties
)
{
    public string Type { get; init; } = "Feature";
}

public record GeoJsonGeometry(
    string Type,
    object Coordinates
);

public record GeoJsonProperties(
    string Id,
    string FeatureType,
    string EntityId,
    string Label,
    string Description,
    string Status,
    string Category,
    string Team,
    int? ScenarioId,
    int? ExecutionId,
    DateTime CreatedAt,
    DateTime? ValidFrom,
    DateTime? ValidTo,
    Dictionary<string, object>? Extra
);

public record GeoJsonFeatureCollection(
    string Type,
    List<GeoJsonFeature> Features
)
{
    public string Type { get; init; } = "FeatureCollection";
}

// ── Map-specific DTOs ──

public record MapLayerInfo(
    string LayerId,
    string Label,
    string FeatureType,
    int FeatureCount,
    bool DefaultVisible
);

public record MapTimelineInfo(
    DateTime? EarliestEvent,
    DateTime? LatestEvent,
    int TotalEvents,
    List<MapTimelineBucket> Buckets
);

public record MapTimelineBucket(
    DateTime Start,
    DateTime End,
    int Count
);

public record MapEntityDetail(
    string EntityId,
    string FeatureType,
    string Label,
    string Description,
    string Status,
    string Category,
    string Team,
    double Latitude,
    double Longitude,
    Dictionary<string, object> Properties,
    List<MapRelatedEntity> RelatedEntities,
    List<MapRecentEvent> RecentEvents
);

public record MapRelatedEntity(
    string EntityId,
    string FeatureType,
    string Label,
    string RelationshipType
);

public record MapRecentEvent(
    int Id,
    DateTime Timestamp,
    string EventType,
    string Description,
    string Severity
);

public record MapSearchResult(
    string EntityId,
    string FeatureType,
    string Label,
    string Category,
    double Latitude,
    double Longitude,
    double Score
);

// ── Write DTOs ──

public record CreateMapFeatureDto(
    string FeatureType,
    string EntityId,
    int? ScenarioId,
    int? ExecutionId,
    string Label,
    string? Description,
    double Latitude,
    double Longitude,
    string? Geometry,
    string? Status,
    string? Category,
    string? Team,
    string? Properties,
    string? SourceFeatureId,
    string? TargetFeatureId,
    DateTime? ValidFrom,
    DateTime? ValidTo
);

public record UpdateMapFeatureDto(
    string? Label,
    string? Description,
    double? Latitude,
    double? Longitude,
    string? Geometry,
    string? Status,
    string? Category,
    string? Team,
    string? Properties,
    string? SourceFeatureId,
    string? TargetFeatureId,
    DateTime? ValidFrom,
    DateTime? ValidTo
);

public record MapFeatureDto(
    int Id,
    string FeatureType,
    string EntityId,
    int? ScenarioId,
    int? ExecutionId,
    string Label,
    string Description,
    double Latitude,
    double Longitude,
    string? Geometry,
    string Status,
    string Category,
    string Team,
    string Properties,
    string? SourceFeatureId,
    string? TargetFeatureId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ValidFrom,
    DateTime? ValidTo
);
