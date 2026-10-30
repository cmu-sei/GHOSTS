// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;

namespace ScenarioBuilder.Api.Domain;

public record QuestionView(string Id, int Stage, string Text, string Why, string? Recommended, AnswerType Answer, List<string>? Options)
{
    public static QuestionView From(KindQuestion q, int stage) => new(q.Id, stage, q.Text, q.Why, q.Recommended, q.Answer, q.Options);
}

public record AnsweredView(string Id, int Stage, JsonNode? Value, Who Who, string By, Via Via, string? Reason, DateTime Time)
{
    public static AnsweredView From(Decision d) =>
        new(d.QuestionId, d.Stage, JsonNode.Parse(d.Value), d.Who, d.DecidedBy, d.Via, d.Reason, d.Time);
}

public record ProposalView(int Id, string QuestionId, JsonNode? Before, JsonNode? After, string Reason, string ProposedBy, ProposalStatus Status)
{
    public static ProposalView From(Proposal p) =>
        new(p.Id, p.QuestionId, p.Before is null ? null : JsonNode.Parse(p.Before), JsonNode.Parse(p.After), p.Reason, p.ProposedBy, p.Status);
}

/// <summary>Something in the way of closing a stage, and the stage it is about. Severity is "stop" or "warning".</summary>
public record Finding(string Severity, string Code, string Message, int Stage);

public record GateView(bool Passes, string? Approval, bool Approved, List<Finding> Findings);

/// <summary>Where a scenario stands: the open stage, what's left in it, and everything answered so far.</summary>
public record ProgressView(
    string KindId,
    int Stage,
    string StageTitle,
    bool LastStage,
    GateView Gate,
    List<QuestionView> Open,
    List<AnsweredView> Answered,
    List<string> DraftsDue,
    bool Delegated)
{
    /// <summary>The context the person gave, so the agent knows what it can search. The progress endpoint fills it.</summary>
    public List<SourceSummary> Sources { get; init; } = [];
}

public record SourceSummary(int Id, string Name, SourceKind Kind, int Characters);
