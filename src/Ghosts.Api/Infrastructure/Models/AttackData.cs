// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

// ──────────────────────────────────────────────
// MITRE ATT&CK Reference Data
// ──────────────────────────────────────────────

[Table("attack_techniques")]
public class AttackTechnique
{
    [Key]
    [MaxLength(20)]
    public string Id { get; set; } = string.Empty; // e.g. "T1059", "T1059.001"

    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Tactics { get; set; } = string.Empty; // comma-separated: "execution,persistence"

    public string Platforms { get; set; } = string.Empty; // comma-separated

    public string DataSources { get; set; } = string.Empty;

    public string Detection { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    public bool IsSubtechnique { get; set; }

    [MaxLength(20)]
    public string ParentId { get; set; } // nullable self-FK

    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AttackTechnique Parent { get; set; }
    public ICollection<AttackTechnique> Subtechniques { get; set; } = new List<AttackTechnique>();
    public ICollection<AttackGroupTechnique> GroupUsages { get; set; } = new List<AttackGroupTechnique>();
}

[Table("attack_groups")]
public class AttackGroup
{
    [Key]
    [MaxLength(20)]
    public string Id { get; set; } = string.Empty; // e.g. "G0007"

    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public string Aliases { get; set; } = string.Empty; // comma-separated

    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<AttackGroupTechnique> TechniqueUsages { get; set; } = new List<AttackGroupTechnique>();
}

[Table("attack_group_techniques")]
public class AttackGroupTechnique
{
    [MaxLength(20)]
    public string GroupId { get; set; }

    [MaxLength(20)]
    public string TechniqueId { get; set; }

    public string Use { get; set; } = string.Empty; // how the group uses the technique

    // Navigation
    public AttackGroup Group { get; set; }
    public AttackTechnique Technique { get; set; }
}

// ──────────────────────────────────────────────
// DTOs
// ──────────────────────────────────────────────

public record AttackTechniqueDto(
    string Id, string Name, string Description,
    string Tactics, string Platforms, string Url,
    bool IsSubtechnique, string ParentId);

public record AttackTechniqueSummaryDto(
    string Id, string Name, string Tactics, bool IsSubtechnique);

public record AttackGroupDto(
    string Id, string Name, string Aliases,
    string Description, string Url,
    List<AttackTechniqueSummaryDto> Techniques);

public record AttackGroupSummaryDto(
    string Id, string Name, string Aliases, int TechniqueCount);
