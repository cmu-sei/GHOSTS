using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ghosts.Api.Infrastructure.Models;

public class Scenario
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ScenarioParameters ScenarioParameters { get; set; }
    public TechnicalEnvironment TechnicalEnvironment { get; set; }
    public GameMechanics GameMechanics { get; set; }
    public ScenarioTimeline ScenarioTimeline { get; set; }
    public ICollection<Execution> Executions { get; set; } = new List<Execution>();
}

[Table("scenario_parameters")]
public class ScenarioParameters
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public string Objectives { get; set; }
    public string PoliticalContext { get; set; }
    public string RulesOfEngagement { get; set; }
    public string VictoryConditions { get; set; }

    // Navigation properties
    public Scenario Scenario { get; set; }
    public ICollection<Nation> Nations { get; set; } = new List<Nation>();
    public ICollection<ThreatActor> ThreatActors { get; set; } = new List<ThreatActor>();
    public ICollection<Inject> Injects { get; set; } = new List<Inject>();
    public ICollection<UserPool> UserPools { get; set; } = new List<UserPool>();
}

public class Nation
{
    public int Id { get; set; }
    public int ScenarioParametersId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Alignment { get; set; } = string.Empty; // friendly, adversary, neutral

    public ScenarioParameters ScenarioParameters { get; set; }
}

[Table("threat_actors")]
public class ThreatActor
{
    public int Id { get; set; }
    public int ScenarioParametersId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // state, criminal, hacktivist, insider
    public int Capability { get; set; }
    public string Ttps { get; set; } // Comma-separated MITRE ATT&CK techniques

    public ScenarioParameters ScenarioParameters { get; set; }
}

public class Inject
{
    public int Id { get; set; }
    public int ScenarioParametersId { get; set; }
    public string Trigger { get; set; } = string.Empty; // e.g., T+10m, OnDetect
    public string Title { get; set; } = string.Empty;

    public ScenarioParameters ScenarioParameters { get; set; }
}

public class UserPool
{
    public int Id { get; set; }
    public int ScenarioParametersId { get; set; }
    public string Role { get; set; } = string.Empty;
    public int Count { get; set; }

    public ScenarioParameters ScenarioParameters { get; set; }
}

[Table("technical_environments")]
public class TechnicalEnvironment
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public string NetworkTopology { get; set; }
    public string Services { get; set; }
    public string Assets { get; set; }
    public string Defenses { get; set; } // JSON array serialized as string

    public Scenario Scenario { get; set; }
    public ICollection<Vulnerability> Vulnerabilities { get; set; } = new List<Vulnerability>();
}

public class Vulnerability
{
    public int Id { get; set; }
    public int TechnicalEnvironmentId { get; set; }
    public string Asset { get; set; } = string.Empty;
    public string Cve { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;

    public TechnicalEnvironment TechnicalEnvironment { get; set; }
}

[Table("game_mechanics")]
public class GameMechanics
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public string TimelineType { get; set; } = "real-time"; // real-time, compressed, turn-based
    public int DurationHours { get; set; }
    public string AdjudicationType { get; set; } = "manual"; // manual, automated, hybrid
    public string EscalationLadder { get; set; }
    public string BranchingLogic { get; set; }
    public bool CollectLogs { get; set; }
    public bool CollectNetwork { get; set; }
    public bool CollectEndpoint { get; set; }
    public bool CollectChat { get; set; }
    public string PerformanceMetrics { get; set; }

    public Scenario Scenario { get; set; }
}

[Table("scenario_timeline")]
public class ScenarioTimeline
{
    public int Id { get; set; }
    public int ScenarioId { get; set; }
    public int ExerciseDuration { get; set; }

    public Scenario Scenario { get; set; }
    public ICollection<ScenarioTimelineEvent> ScenarioTimelineEvents { get; set; } = new List<ScenarioTimelineEvent>();
}

[Table("scenario_timeline_events")]
public class ScenarioTimelineEvent
{
    public int Id { get; set; }
    public int ScenarioTimelineId { get; set; }
    public string Time { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Assigned { get; set; } = string.Empty; // White Cell, Red Team, Blue Team, Green Cell
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Active, Complete

    public ScenarioTimeline Timeline { get; set; }
}

// Request/Response DTOs
public record ScenarioDto(
    int Id,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    ScenarioParametersDto ScenarioParameters,
    TechnicalEnvironmentDto TechnicalEnvironment,
    GameMechanicsDto GameMechanics,
    TimelineDto Timeline
);

public record CreateScenarioDto(
    string Name,
    string Description,
    ScenarioParametersDto ScenarioParameters,
    TechnicalEnvironmentDto TechnicalEnvironment,
    GameMechanicsDto GameMechanics,
    TimelineDto Timeline
);

public record UpdateScenarioDto(
    string Name,
    string Description,
    ScenarioParametersDto ScenarioParameters,
    TechnicalEnvironmentDto TechnicalEnvironment,
    GameMechanicsDto GameMechanics,
    TimelineDto Timeline
);

public record ScenarioParametersDto(
    List<NationDto> Nations,
    List<ThreatActorDto> ThreatActors,
    List<InjectDto> Injects,
    List<UserPoolDto> UserPools,
    string Objectives,
    string PoliticalContext,
    string RulesOfEngagement,
    string VictoryConditions
);

public record NationDto(string Name, string Alignment);

public record ThreatActorDto(string Name, string Type, int Capability, List<string> Ttps);

public record InjectDto(string Trigger, string Title);

public record UserPoolDto(string Role, int Count);

public record TechnicalEnvironmentDto(
    string NetworkTopology,
    string Services,
    string Assets,
    List<string> Defenses,
    List<VulnerabilityDto> Vulnerabilities
);

public record VulnerabilityDto(string Asset, string Cve, string Severity);

public record GameMechanicsDto(
    string TimelineType,
    int DurationHours,
    string AdjudicationType,
    string EscalationLadder,
    string BranchingLogic,
    TelemetryDto Telemetry,
    string PerformanceMetrics
);

public record TelemetryDto(
    bool CollectLogs,
    bool CollectNetwork,
    bool CollectEndpoint,
    bool CollectChat
);

public record TimelineDto(
    int ExerciseDuration,
    List<TimelineEventDto> Events
);

public record TimelineEventDto(
    string Time,
    int Number,
    string Assigned,
    string Description,
    string Status
);
