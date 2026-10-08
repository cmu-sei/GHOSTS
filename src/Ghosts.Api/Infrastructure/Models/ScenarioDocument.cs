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
/// GHOSTS runs; the document is what a person wrote, byte for byte. One row per import, so a scenario
/// keeps its history: the current document is the newest row.
/// </summary>
[Table("scenario_documents")]
public class ScenarioDocument
{
    public const string Imported = "import";
    public const string Backfilled = "backfill";
    /// <summary>Derived from the rows the Scenario Builder's compile wrote, validated with 0 errors before they were kept.</summary>
    public const string Compiled = "compile";

    public int Id { get; set; }
    public int ScenarioId { get; set; }

    /// <summary>The schema version the document declares, e.g. 1.1.0.</summary>
    [MaxLength(20)]
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>SHA-256 of the canonical document bytes, lower-case hex.</summary>
    [MaxLength(64)]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of the document the rows derived to when this one was stored. While they still do,
    /// nothing has edited the scenario and export returns this document; after an edit, the rows.
    /// </summary>
    [MaxLength(64)]
    public string RowsHash { get; set; } = string.Empty;

    /// <summary>The canonical document, exactly as imported.</summary>
    [Column(TypeName = "jsonb")]
    public string Document { get; set; } = "{}";

    /// <summary>What the validator found for this import, with its version and the time it ran.</summary>
    [Column(TypeName = "jsonb")]
    public string Validation { get; set; } = "{}";

    /// <summary>
    /// "import" for a document someone approved and imported, "backfill" for one derived from rows at
    /// startup. Null on rows stored before this was recorded. The newest import is the approved version.
    /// </summary>
    [MaxLength(20)]
    public string Origin { get; set; }

    /// <summary>The document the rows derived to when this one was stored, so that an edit since can be listed (A5).</summary>
    [Column(TypeName = "jsonb")]
    public string RowsDocument { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Scenario Scenario { get; set; }
}
