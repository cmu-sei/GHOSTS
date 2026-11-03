// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;

namespace ScenarioBuilder.Api.Domain;

public record CarriedAnswer(string Id, string Label, int FromStage, int ToStage);

public record DroppedAnswer(string Id, string Label, string Why);

public record NewQuestion(string Id, string Label, int Stage);

/// <summary>
/// What moving a scenario from version <see cref="From"/> of its scenario type to <see cref="To"/> does, worked out before
/// anything changes: the answers that carry over, the ones dropped and why, the questions new to it, the drafted
/// fields the new version no longer has, where the open stage ends up, and the owner, who alone can apply it.
/// </summary>
public record ScenarioTypeUpdatePlan(
    string From, string To, List<CarriedAnswer> Carried, List<DroppedAnswer> Dropped, List<NewQuestion> Added, List<string> DraftsDropped,
    int StageFrom, int StageTo, string Owner);

public static class ScenarioTypeUpdate
{
    /// <summary>
    /// An answer carries over when its question, matched by id, is still in <paramref name="to"/> and the answer still
    /// fits it; it moves to the stage the question is in now. Every other answer is dropped. A question new to the scenario type
    /// is simply open: the ledger asks it even in a stage that has closed. The open stage stays, within the new stages.
    /// </summary>
    public static ScenarioTypeUpdatePlan Plan(ScenarioType from, ScenarioType to, Scenario scenario, IReadOnlyList<Decision> decisions)
    {
        var carried = new List<CarriedAnswer>();
        var dropped = new List<DroppedAnswer>();
        var byQuestion = decisions.ToDictionary(d => d.QuestionId);
        var order = from.AllQuestions.Select(q => q.Id).Concat(decisions.Select(d => d.QuestionId)).Distinct();
        foreach (var questionId in order)
        {
            if (!byQuestion.TryGetValue(questionId, out var decision))
                continue;
            var label = from.Find(questionId)?.Question.Label ?? questionId;
            if (to.Find(questionId) is not { } now)
                dropped.Add(new DroppedAnswer(questionId, label, $"Its question isn't in version {to.Version}."));
            else if (Ledger.ValidateValue(now.Question, JsonNode.Parse(decision.Value), out _) is { } problem)
                dropped.Add(new DroppedAnswer(questionId, label, $"The question changed, and the answer doesn't fit it now: {problem}"));
            else
                carried.Add(new CarriedAnswer(questionId, now.Question.Label ?? questionId, decision.Stage, now.Stage));
        }

        var known = from.AllQuestions.Select(q => q.Id).ToHashSet();
        var added = to.Stages.SelectMany(s => s.Questions.Where(q => !known.Contains(q.Id)).Select(q => new NewQuestion(q.Id, q.Label ?? q.Id, s.Number))).ToList();
        var fields = to.Drafts.Select(d => d.Field).ToHashSet();
        var draftsDropped = Ledger.ReadDrafts(scenario).Where(f => !fields.Contains(f.Key) && f.Value is not null).Select(f => f.Key).ToList();
        return new ScenarioTypeUpdatePlan(from.Version, to.Version, carried, dropped, added, draftsDropped, scenario.Stage, Math.Min(scenario.Stage, to.LastStage), scenario.Owner);
    }
}
