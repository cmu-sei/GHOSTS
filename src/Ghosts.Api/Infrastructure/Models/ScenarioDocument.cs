// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

// ──────────────────────────────────────────────
// Scenario Document (the authored specification, kept whole)
// ──────────────────────────────────────────────

/// <summary>
/// One scenario document as it was imported, beside the rows it was mapped onto. The rows are what
/// GHOSTS runs; the document is what a person wrote, and it holds everything the columns have no
/// place for (see STORAGE_LOSSY). One row per import, so a scenario keeps its history: the current
/// document is the newest row.
/// </summary>
[Table("scenario_documents")]
public class ScenarioDocument
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }

    /// <summary>The schema version the document declares, e.g. 1.1.0.</summary>
    [MaxLength(20)]
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>SHA-256 of the canonical document bytes, lower-case hex.</summary>
    [MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>The canonical document, exactly as imported.</summary>
    [Column(TypeName = "jsonb")]
    public string Document { get; set; } = "{}";

    /// <summary>What the validator found for this import, with its version and the time it ran.</summary>
    [Column(TypeName = "jsonb")]
    public string Validation { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
}
