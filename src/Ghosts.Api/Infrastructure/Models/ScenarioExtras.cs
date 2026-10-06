// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ghosts.Api.Infrastructure.Models;

/// <summary>
/// What a scenario document says about a row that none of its columns hold, kept in the row's extras
/// (jsonb) in the document's own shape: import stores an item's leftover keys as they are, export merges
/// them back, and the scenario screens edit them. The records mirror schemas/scenario-document/v1 —
/// their camelCase names are the document's keys. Typed rather than raw JSON because PUT reads with
/// Newtonsoft and the hub with System.Text.Json, and neither reads the other's JSON object type.
/// </summary>
public static class ScenarioExtras
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Write<T>(T extras) where T : class =>
        extras == null ? null : JsonSerializer.Serialize(extras, Options);

    public static T Read<T>(string json) where T : class =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<T>(json, Options);
}

/// <summary>The scenario's own: everything at document level that no column holds.</summary>
public record ScenarioExtrasDto(
    string Slug = null,
    string Intent = null,
    CatalogDto Catalog = null,
    ContextExtrasDto Context = null,
    AudienceExtrasDto Audience = null,
    TerrainExtrasDto Terrain = null,
    StartingConditionsDto StartingConditions = null,
    RulesOfPlayExtrasDto RulesOfPlay = null,
    List<DocumentSourceDto> Sources = null,
    List<DocumentReferenceDto> References = null);

public record CatalogDto(bool? Listed = null, int? SortOrder = null, string Era = null, string Theater = null, int? EstimatedMinutes = null);

public record ContextExtrasDto(string Situation = null);

public record AudienceExtrasDto(string Role = null, int? Size = null, string Proficiency = null, string Mandate = null);

/// <summary>Defenses carry their names here too; export matches them to the defense column by name.</summary>
public record TerrainExtrasDto(
    TerrainReferenceDto Reference = null,
    List<SegmentDto> Segments = null,
    List<HostDto> Hosts = null,
    List<TerrainServiceDto> Services = null,
    InformationEnvironmentDto InformationEnvironment = null,
    List<DefenseDto> Defenses = null);

public record TerrainReferenceDto(string Provider = null, string Slice = null);

public record SegmentDto(string Name = null, string Cidr = null, string Description = null);

public record HostDto(string Name = null, string Segment = null, string Os = null, string Role = null, string Description = null, List<string> Services = null);

public record TerrainServiceDto(string Name = null, string Description = null, List<string> Hosts = null);

public record InformationEnvironmentDto(string Platforms = null, string Audience = null);

public record DefenseDto(string Name = null, string Description = null, List<string> Covers = null);

public record StartingConditionsDto(List<string> Flags = null, Dictionary<string, string> Facts = null);

public record RulesOfPlayExtrasDto(ClockDto Clock = null, DeadlineDto Deadline = null, string Fog = null, EscalationLadderExtrasDto EscalationLadder = null);

public record ClockDto(int? TickMinutes = null, string Label = null);

public record DeadlineDto(string At = null, string Label = null, string DecisiveAction = null, string Warning = null, string FailureMessage = null);

public record EscalationLadderExtrasDto(List<RungDto> Rungs = null);

public record RungDto(string Name = null, string Description = null, bool? Recoverable = null);

public record DocumentSourceDto(string Id = null, string Name = null, string Type = null, string Uri = null, string MimeType = null, string Note = null);

public record DocumentReferenceDto(string Id = null, string Title = null, string Uri = null, string Locator = null, string Note = null);

/// <summary>A threat actor's: the document's adversary id, objective, win threshold and playbook.</summary>
public record AdversaryExtrasDto(string Id = null, string Objective = null, int? WinThreshold = null, List<MoveDto> Playbook = null);

public record MoveDto(
    string Id = null,
    string Domain = null,
    string Description = null,
    List<string> Techniques = null,
    string Preconditions = null,
    int? Progress = null,
    EffectsDto Effects = null,
    List<string> Indicators = null);

public record EffectsDto(List<string> SetFlags = null, Dictionary<string, string> SetFacts = null);

public record PoolExtrasDto(string Description = null);

/// <summary>A vulnerability's description, when the cve column already holds its CVE.</summary>
public record VulnerabilityExtrasDto(string Description = null);

public record ObjectiveExtrasDto(string MetWhen = null);

/// <summary>
/// An event's. A timeline-event row has columns for its time, owner, objectives, schedule, condition and
/// execution, so only id, title, expectedResponse, effects and indicators land here; an inject row holds
/// only a time and a title, so the rest of the event lands here.
/// </summary>
public record EventExtrasDto(
    string Id = null,
    string Title = null,
    string Owner = null,
    List<int> Objectives = null,
    string Schedule = null,
    string When = null,
    EventExecutionDto Execution = null,
    string ExpectedResponse = null,
    EffectsDto Effects = null,
    List<string> Indicators = null);

public record EventExecutionDto(string Mode = null, string WorkflowRef = null);
