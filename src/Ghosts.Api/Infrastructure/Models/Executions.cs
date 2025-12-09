#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

/// <summary>
/// Represents a specific execution/run of a Scenario template.
/// Multiple executions can be created from the same scenario with different parameters.
/// </summary>
[Table("executions")]
public class Execution
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the Scenario template being executed
    /// </summary>
    public int ScenarioId { get; set; }

    /// <summary>
    /// Custom name for this execution run (e.g., "APT Exercise - Team Alpha")
    /// If not provided, defaults to "{Scenario.Name} - Run {N}"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Notes or description specific to this execution
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the execution
    /// </summary>
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Created;

    /// <summary>
    /// When this execution was created/configured
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the execution actually started running
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the execution completed (successfully or failed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// JSON-serialized parameter overrides for this specific execution.
    /// Allows varying parameters like time scales, weights, random seeds, etc.
    /// Example: {"timeScale": 2.0, "engagementMultiplier": 1.5, "randomSeed": 12345}
    /// </summary>
    public string ParameterOverrides { get; set; } = "{}";

    /// <summary>
    /// JSON-serialized execution configuration.
    /// Example: {"autoStart": true, "pauseOnError": false, "recordDetailedMetrics": true}
    /// </summary>
    public string Configuration { get; set; } = "{}";

    /// <summary>
    /// JSON-serialized runtime metrics collected during execution.
    /// Updated as the execution runs.
    /// Example: {"npcActionsProcessed": 1250, "postsCreated": 342, "engagementScore": 8.7}
    /// </summary>
    public string Metrics { get; set; } = "{}";

    /// <summary>
    /// JSON-serialized error information if execution failed
    /// Example: {"error": "NPCInitializationException", "message": "Failed to load...", "timestamp": "..."}
    /// </summary>
    public string ErrorDetails { get; set; } = "{}";

    // Navigation properties
    public Scenario Scenario { get; set; } = null!;
    public ICollection<ExecutionEvent> Events { get; set; } = new List<ExecutionEvent>();
    public ICollection<ExecutionMetricSnapshot> MetricSnapshots { get; set; } = new List<ExecutionMetricSnapshot>();
}

/// <summary>
/// Status of an execution lifecycle
/// </summary>
public enum ExecutionStatus
{
    /// <summary>
    /// Execution has been created but not started
    /// </summary>
    Created,

    /// <summary>
    /// Execution is currently running
    /// </summary>
    Running,

    /// <summary>
    /// Execution has been paused by user or system
    /// </summary>
    Paused,

    /// <summary>
    /// Execution completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Execution failed with errors
    /// </summary>
    Failed,

    /// <summary>
    /// Execution was cancelled by user
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a specific event that occurred during an execution.
/// Used for audit trail and debugging.
/// </summary>
[Table("execution_events")]
public class ExecutionEvent
{
    public int Id { get; set; }
    public int ExecutionId { get; set; }

    /// <summary>
    /// When this event occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Type of event (e.g., "StatusChange", "InjectFired", "Error", "UserAction")
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the event
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialized event data/context
    /// </summary>
    public string Data { get; set; } = "{}";

    /// <summary>
    /// Severity level (Info, Warning, Error)
    /// </summary>
    public string Severity { get; set; } = "Info";

    public Execution Execution { get; set; } = null!;
}

/// <summary>
/// Point-in-time snapshot of execution metrics.
/// Enables time-series analysis and visualization.
/// </summary>
[Table("execution_metric_snapshots")]
public class ExecutionMetricSnapshot
{
    public int Id { get; set; }
    public int ExecutionId { get; set; }

    /// <summary>
    /// When this snapshot was captured
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Elapsed time since execution start (in seconds)
    /// </summary>
    public int ElapsedSeconds { get; set; }

    /// <summary>
    /// JSON-serialized metrics at this point in time
    /// Example: {
    ///   "activeNpcs": 1000,
    ///   "totalPosts": 523,
    ///   "totalEngagements": 1847,
    ///   "viralPosts": 12,
    ///   "averageEngagementRate": 0.087,
    ///   "cpuUsagePercent": 45.2,
    ///   "memoryUsageMB": 2048
    /// }
    /// </summary>
    public string Metrics { get; set; } = "{}";

    public Execution Execution { get; set; } = null!;
}

// Request/Response DTOs

public record ExecutionDto(
    int Id,
    int ScenarioId,
    string ScenarioName, // Denormalized for convenience
    string Name,
    string Description,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    string ParameterOverrides,
    string Configuration,
    string Metrics,
    string ErrorDetails
);

public record CreateExecutionDto(
    int ScenarioId,
    string? Name,
    string? Description,
    string? ParameterOverrides,
    string? Configuration
);

public record UpdateExecutionDto(
    string? Name,
    string? Description,
    string? ParameterOverrides,
    string? Configuration
);

public record ExecutionEventDto(
    int Id,
    DateTime Timestamp,
    string EventType,
    string Description,
    string Data,
    string Severity
);

public record CreateExecutionEventDto(
    string EventType,
    string Description,
    string? Data,
    string? Severity
);

public record ExecutionMetricSnapshotDto(
    int Id,
    DateTime Timestamp,
    int ElapsedSeconds,
    string Metrics
);

/// <summary>
/// Summary statistics for an execution
/// </summary>
public record ExecutionSummaryDto(
    int Id,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DurationSeconds,
    string ScenarioName,
    int EventCount,
    int SnapshotCount
);
