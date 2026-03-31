// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

// ──────────────────────────────────────────────
// Scenario Source (uploaded documents / text / URLs)
// ──────────────────────────────────────────────

[Table("scenario_sources")]
public class ScenarioSource
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }

    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SourceType { get; set; } = "Text"; // Document, Text, Url

    [MaxLength(200)]
    public string MimeType { get; set; }

    [MaxLength(500)]
    public string OriginalFileName { get; set; }

    public string Content { get; set; } = string.Empty; // raw text content
    public byte[] FileData { get; set; } // raw file bytes (nullable)
    public long FileSizeBytes { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Uploaded"; // Uploaded, Chunking, Chunked, Extracting, Extracted, Failed

    public string ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
    public ICollection<ScenarioSourceChunk> Chunks { get; set; } = new List<ScenarioSourceChunk>();
}

// ──────────────────────────────────────────────
// Scenario Source Chunks (preprocessed text segments)
// ──────────────────────────────────────────────

[Table("scenario_source_chunks")]
public class ScenarioSourceChunk
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public int ScenarioId { get; set; } // denormalized for fast queries

    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }

    [MaxLength(50)]
    public string ExtractionStatus { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ScenarioSource Source { get; set; }
}

// ──────────────────────────────────────────────
// Scenario Entity (extracted / operator / enriched nodes)
// ──────────────────────────────────────────────

[Table("scenario_entities")]
public class ScenarioEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ScenarioId { get; set; }

    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string EntityType { get; set; } = "Custom"; // Person, Organization, System, Network, Location, Software, ThreatActor, Campaign, Vulnerability, DataAsset, Service, Custom

    public string Description { get; set; }

    [Column(TypeName = "jsonb")]
    public string Properties { get; set; } = "{}"; // arbitrary key-value metadata

    [Column(TypeName = "decimal(5,4)")]
    public decimal Confidence { get; set; } = 1.0m;

    [MaxLength(50)]
    public string Origin { get; set; } = "Operator"; // Extracted, Operator, Enriched, Generated

    // Provenance
    public int? SourceId { get; set; }
    public int? SourceChunkId { get; set; }

    // Link to synthesized NPC
    public Guid? NpcId { get; set; }

    [MaxLength(200)]
    public string ExternalId { get; set; } // ATT&CK ID, CVE, etc.

    public bool IsReviewed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
    public ScenarioSource Source { get; set; }
    public ScenarioSourceChunk SourceChunk { get; set; }
    public NpcRecord Npc { get; set; }
    public ICollection<ScenarioEdge> OutgoingEdges { get; set; } = new List<ScenarioEdge>();
    public ICollection<ScenarioEdge> IncomingEdges { get; set; } = new List<ScenarioEdge>();
}

// ──────────────────────────────────────────────
// Scenario Edge (relationships between entities)
// ──────────────────────────────────────────────

[Table("scenario_edges")]
public class ScenarioEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ScenarioId { get; set; }

    public Guid SourceEntityId { get; set; }
    public Guid TargetEntityId { get; set; }

    [MaxLength(50)]
    public string EdgeType { get; set; } = "Custom"; // MemberOf, Targets, Exploits, Uses, LocatedAt, CommunicatesWith, DependsOn, Accesses, Owns, ReportsTo, AffiliatedWith, DefendedBy, CommandsAndControl, Custom

    [MaxLength(500)]
    public string Label { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal Weight { get; set; } = 1.0m;

    [Column(TypeName = "decimal(5,4)")]
    public decimal Confidence { get; set; } = 1.0m;

    [MaxLength(50)]
    public string Origin { get; set; } = "Operator"; // Extracted, Operator, Enriched

    // Provenance
    public int? SourceId { get; set; }
    public int? SourceChunkId { get; set; }

    [Column(TypeName = "jsonb")]
    public string Properties { get; set; } = "{}";

    public bool IsReviewed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
    public ScenarioEntity SourceEntity { get; set; }
    public ScenarioEntity TargetEntity { get; set; }
}

// ──────────────────────────────────────────────
// Scenario Enrichment (ATT&CK / threat intel records)
// ──────────────────────────────────────────────

[Table("scenario_enrichments")]
public class ScenarioEnrichment
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public Guid? EntityId { get; set; }

    [MaxLength(50)]
    public string EnrichmentType { get; set; } = string.Empty; // AttackTechnique, AttackGroup, CveDetail, ThreatIntel

    [MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty; // T1059, G0007, etc.

    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string Data { get; set; } = "{}"; // full enrichment payload

    [MaxLength(50)]
    public string Source { get; set; } = "Custom"; // MitreAttack, Nvd, Custom

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
    public ScenarioEntity Entity { get; set; }
}

// ──────────────────────────────────────────────
// Scenario Compilation (compiled scenario snapshots)
// ──────────────────────────────────────────────

[Table("scenario_compilations")]
public class ScenarioCompilation
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }

    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Compiling, Completed, Failed

    [Column(TypeName = "jsonb")]
    public string PackageData { get; set; } = "{}"; // compiled GHOSTS-native scenario package

    public int NpcCount { get; set; }
    public int TimelineEventCount { get; set; }
    public int InjectCount { get; set; }

    public string ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Scenario Scenario { get; set; }
}

// ──────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────

// Source DTOs
public record CreateScenarioSourceTextDto(string Name, string Content);
public record CreateScenarioSourceUrlDto(string Name, string Url);

public record ScenarioSourceDto(
    int Id, string Name, string SourceType, string MimeType,
    string OriginalFileName, long FileSizeBytes, string Status,
    string ErrorMessage, DateTime CreatedAt, int ChunkCount);

public record ScenarioSourceChunkDto(
    int Id, int SourceId, int ChunkIndex, string Content,
    int TokenCount, string ExtractionStatus, DateTime CreatedAt);

// Entity DTOs
public record ScenarioEntityDto(
    Guid Id, string Name, string EntityType, string Description,
    string Properties, decimal Confidence, string Origin,
    int? SourceId, Guid? NpcId, string ExternalId,
    bool IsReviewed, DateTime CreatedAt);

public record CreateScenarioEntityDto(
    string Name, string EntityType, string Description,
    string Properties, decimal Confidence);

public record UpdateScenarioEntityDto(
    string Name, string EntityType, string Description,
    string Properties, decimal Confidence, bool IsReviewed);

// Edge DTOs
public record ScenarioEdgeDto(
    Guid Id, Guid SourceEntityId, Guid TargetEntityId,
    string EdgeType, string Label, decimal Weight,
    decimal Confidence, string Origin, bool IsReviewed);

public record CreateScenarioEdgeDto(
    Guid SourceEntityId, Guid TargetEntityId,
    string EdgeType, string Label, decimal Weight, decimal Confidence);

// Graph DTO
public record ScenarioGraphDto(
    List<ScenarioEntityDto> Nodes,
    List<ScenarioEdgeDto> Edges);

// Enrichment DTOs
public record ScenarioEnrichmentDto(
    int Id, Guid? EntityId, string EnrichmentType,
    string ExternalId, string Name, string Description,
    string Data, string Source, DateTime CreatedAt);

public record ApplyAttackEnrichmentDto(Guid EntityId, string TechniqueId);
public record ApplyGroupEnrichmentDto(Guid EntityId, string GroupId);

public record SuggestedEnrichment(
    Guid EntityId, string TechniqueId, string TechniqueName,
    decimal Relevance, string Reasoning);

public record SuggestEnrichmentsResponseDto(List<SuggestedEnrichment> Suggestions);

// Compilation DTOs
public record ScenarioCompilationDto(
    int Id, string Name, string Status, int NpcCount,
    int TimelineEventCount, int InjectCount,
    DateTime CreatedAt, DateTime? CompletedAt, string ErrorMessage);

public record CompileScenarioDto(
    string Name, bool GenerateNpcs = true,
    bool GenerateTimeline = true, bool MapAttackToInjects = true);

// Assistant DTOs
public record AssistantMessageDto(string Role, string Content);
public record AssistantRequestDto(string Message, List<AssistantMessageDto> History);
public record AssistantResponseDto(string Response, string Action, object ActionData);

// Extraction result
public record ExtractionResultDto(int EntitiesCreated, int EdgesCreated, int ChunksProcessed, List<string> Errors);

// ──────────────────────────────────────────────
// NPC-to-Machine assignment (scoped to a compilation)
// ──────────────────────────────────────────────

[Table("scenario_npc_assignments")]
public class ScenarioNpcAssignment
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public int CompilationId { get; set; }

    /// <summary>NPC that will be deployed</summary>
    public Guid NpcId { get; set; }

    /// <summary>Registered machine that will execute the NPC's timeline</summary>
    public Guid MachineId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
    public ScenarioCompilation Compilation { get; set; }
}

// NPC Assignment DTOs
public record NpcAssignmentDto(
    int Id, int CompilationId,
    Guid NpcId, string NpcName,
    Guid MachineId, string MachineName,
    DateTime CreatedAt);

public record CreateNpcAssignmentDto(Guid NpcId, Guid MachineId);

public record NpcForAssignmentDto(
    Guid NpcId, string NpcName, string EntityName,
    Guid? AssignedMachineId, string AssignedMachineName,
    int? AssignmentId);

public record DeploymentReadinessDto(
    bool IsReady, int TotalNpcs, int AssignedNpcs, int UnassignedNpcs,
    List<string> Issues);
