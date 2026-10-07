// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

// ──────────────────────────────────────────────
// Scenario authoring: a conversation with a model that drafts a scenario document
// ──────────────────────────────────────────────

/// <summary>The ScenarioAuthoring section of appsettings.json.</summary>
public class ScenarioAuthoringOptions
{
    public string Region { get; set; } = "us-east-1";
    public string Model { get; set; } = string.Empty;

    /// <summary>The models a developer may pick for a new session; Model is the one picked by default.</summary>
    public List<AuthoringModelChoice> Models { get; set; } = [];

    /// <summary>
    /// The model a new session starts on, when it names none, for ten minutes after Model was unavailable
    /// (F4). A session that has begun keeps its model (C5). Empty: new sessions always start on Model.
    /// </summary>
    public string FallbackModel { get; set; } = string.Empty;

    public int MaxOutputTokens { get; set; } = 64000;

    /// <summary>H2: cache points on the system prompt, the tools and the conversation. Off for a model that rejects them.</summary>
    public bool PromptCaching { get; set; } = true;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public int TurnTimeoutMinutes { get; set; } = 30;
    public int ValidatorTimeoutSeconds { get; set; } = 90;
}

public class AuthoringModelChoice
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// H3: the effort levels this model takes (low, medium, high, xhigh, max on current Anthropic models), sent
    /// as the request's output_config.effort. Empty: the model takes none, and the page offers none.
    /// </summary>
    public List<string> Efforts { get; set; } = [];

    /// <summary>What a session on this model costs, estimated from the provider's own token counts. Null: no estimate shown.</summary>
    public AuthoringModelPricing Pricing { get; set; }
}

/// <summary>US dollars per million tokens, by the kind of token the provider reports. List prices; a deployment's rate may differ.</summary>
public class AuthoringModelPricing
{
    public decimal InputPerMillion { get; set; }
    public decimal OutputPerMillion { get; set; }
    public decimal CacheReadPerMillion { get; set; }
    public decimal CacheWritePerMillion { get; set; }

    public decimal Estimate(long input, long output, long cacheRead, long cacheWrite) =>
        (input * InputPerMillion + output * OutputPerMillion + cacheRead * CacheReadPerMillion + cacheWrite * CacheWritePerMillion) / 1_000_000m;
}

[Table("authoring_sessions")]
public class AuthoringSession
{
    public Guid Id { get; set; }

    /// <summary>Fixed when the session is created; every call of the session uses it (C5).</summary>
    [MaxLength(200)]
    public string Model { get; set; } = string.Empty;

    /// <summary>H3: the model's effort level for every call of the session, or null for the model's default.</summary>
    [MaxLength(20)]
    public string Effort { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "open";

    /// <summary>The Scenario Builder's scenario this session imports into, replacing its contents. Null: a new scenario.</summary>
    public int? ScenarioId { get; set; }

    /// <summary>
    /// What the server tells the model at the start of the developer's next message: the last turn's gate
    /// report, and any import report or refusal since. Cleared when a turn that delivered it succeeds.
    /// </summary>
    public string PendingNote { get; set; } = string.Empty;

    public int? ImportedScenarioId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<AuthoringMessage> Messages { get; set; } = [];
    public List<AuthoringDocument> Documents { get; set; } = [];
    public List<AuthoringTurn> Turns { get; set; } = [];
}

/// <summary>
/// One turn as the developer sees it: their message, and once it ends, the result the page shows under
/// the reply (status, gate report, changes, failure). A turn runs in the background, so this row is what
/// a reload shows. A row with no EndedAt whose turn is not running was cut off by a restart.
/// </summary>
[Table("authoring_turns")]
public class AuthoringTurn
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public int Turn { get; set; }

    /// <summary>The developer's message, without the server's note before it.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>The turn's result as JSON; null while it runs.</summary>
    [Column(TypeName = "jsonb")]
    public string Result { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }

    public AuthoringSession Session { get; set; }
}

/// <summary>
/// One message of a turn: the developer's, a model reply, or the tool results sent back. A model reply
/// row also records that call's usage from the provider's response (H1). Rows of a turn that failed are
/// kept for the record with InHistory false, and the model never sees them again (C4).
/// </summary>
[Table("authoring_messages")]
public class AuthoringMessage
{
    public const string Developer = "developer";
    public const string ModelRole = "model";
    public const string Tool = "tool";

    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public int Turn { get; set; }

    /// <summary>Order within the turn.</summary>
    public int Sequence { get; set; }

    [MaxLength(20)]
    public string Role { get; set; } = Developer;

    /// <summary>The content blocks exactly as sent or received, reasoning included, as JSON.</summary>
    [Column(TypeName = "jsonb")]
    public string Content { get; set; } = "[]";

    public bool InHistory { get; set; }

    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CacheReadTokens { get; set; }
    public int? CacheWriteTokens { get; set; }

    [MaxLength(40)]
    public string StopReason { get; set; }

    /// <summary>For a model call that failed: the provider's exception and message.</summary>
    public string Error { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AuthoringSession Session { get; set; }
}

/// <summary>
/// One validation of a document in a session, saved the moment the validator returns (C2): the exact text
/// the model passed, its hash, and what the validator found. The latest row is the session's latest
/// validated document; only it can be imported, and only once it has been shown (A2, A3).
/// </summary>
[Table("authoring_documents")]
public class AuthoringDocument
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public int Turn { get; set; }

    /// <summary>The first 12 hex digits of the SHA-256 of the document text's UTF-8 bytes.</summary>
    [MaxLength(12)]
    public string Hash { get; set; } = string.Empty;

    /// <summary>Exactly what the model passed to the validator.</summary>
    public string Document { get; set; } = string.Empty;

    public int Errors { get; set; }
    public int Warnings { get; set; }

    [Column(TypeName = "jsonb")]
    public string Findings { get; set; } = "[]";

    public bool DryRun { get; set; }

    /// <summary>The document this one was compared with: a patch's base, or the session's latest before it.</summary>
    [MaxLength(12)]
    public string BaseHash { get; set; }

    /// <summary>The change list from the base to this document, computed by the server (D2), as a JSON array of lines.</summary>
    [Column(TypeName = "jsonb")]
    public string Changes { get; set; } = "[]";

    /// <summary>
    /// Whether the server has returned this document to the developer: with the reply of the turn that
    /// validated it, or when they opened it on the panel (A3). Only a shown document can be imported.
    /// </summary>
    public bool Shown { get; set; }
    public int? ImportedScenarioId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AuthoringSession Session { get; set; }
}
