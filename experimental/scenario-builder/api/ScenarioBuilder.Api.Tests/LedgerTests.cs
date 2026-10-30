// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Tests;

public class LedgerTests
{
    private static KindQuestion Text(string id) => new(id, $"{id}?", "w", null, AnswerType.Text, null, $"/scenario/{id}");

    // A kind whose stage 0 gained question "b" after a scenario had already passed it.
    private static readonly Domain.Kind Grown = new("grown", "2", "Grown",
    [
        new KindStage(0, "First", [Text("a"), Text("b")], new KindGate("Confirm the first stage")),
        new KindStage(1, "Second", [Text("c")], new KindGate(null))
    ], []);

    private static Decision Answer(string id, int stage) =>
        new() { QuestionId = id, Stage = stage, Value = "\"x\"", Who = Who.Stated, DecidedBy = "dev", Via = Via.Person, Time = DateTime.UtcNow };

    [Fact]
    public void A_question_added_to_a_closed_stage_is_open_again_before_the_open_stage()
    {
        var scenario = new Scenario { Stage = 1 };
        var decisions = new List<Decision> { Answer("a", 0) };
        var approvals = new List<Approval> { new() { Stage = 0, Hash = "approved-before-b-existed" } };

        var progress = Ledger.Progress(Grown, scenario, decisions, approvals);

        Assert.Equal([("b", 0), ("c", 1)], progress.Open.Select(q => (q.Id, q.Stage)));
        Assert.Equal([("QUESTION_OPEN", 0), ("QUESTION_OPEN", 1)], progress.Gate.Findings.Select(f => (f.Code, f.Stage)));
    }

    [Fact]
    public void A_closed_stage_asks_for_approval_again_once_its_new_questions_are_answered()
    {
        var scenario = new Scenario { Stage = 1 };
        var decisions = new List<Decision> { Answer("a", 0), Answer("b", 0), Answer("c", 1) };
        var approvals = new List<Approval> { new() { Stage = 0, Hash = "approved-before-b-existed" } };

        var progress = Ledger.Progress(Grown, scenario, decisions, approvals);

        Assert.Empty(progress.Open);
        Assert.Equal([("APPROVAL_STALE", 0)], progress.Gate.Findings.Select(f => (f.Code, f.Stage)));
    }
}
