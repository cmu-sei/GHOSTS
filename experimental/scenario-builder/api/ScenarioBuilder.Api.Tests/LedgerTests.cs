// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Tests;

public class LedgerTests
{
    private static ScenarioTypeQuestion Text(string id) => new(id, $"{id}?", "w", null, AnswerType.Text, null, $"/scenario/{id}");

    // A scenario type whose stage 0 gained question "b" after a scenario had already passed it.
    private static readonly Domain.ScenarioType Grown = new("grown", "2", "Grown",
    [
        new ScenarioTypeStage(0, "First", [Text("a"), Text("b")]),
        new ScenarioTypeStage(1, "Second", [Text("c")])
    ], []);

    private static Decision Answer(string id, int stage) =>
        new() { QuestionId = id, Stage = stage, Value = "\"x\"", Who = Who.Stated, DecidedBy = "dev", Via = Via.Person, Time = DateTime.UtcNow };

    [Fact]
    public void A_question_added_to_a_closed_stage_is_open_again_before_the_open_stage()
    {
        var scenario = new Scenario { Stage = 1 };
        var decisions = new List<Decision> { Answer("a", 0) };

        var progress = Ledger.Progress(Grown, scenario, decisions);

        Assert.Equal([("b", 0), ("c", 1)], progress.Open.Select(q => (q.Id, q.Stage)));
        Assert.Equal([("QUESTION_OPEN", 0), ("QUESTION_OPEN", 1)], progress.Gate.Findings.Select(f => (f.Code, f.Stage)));
    }

    [Fact]
    public void A_closed_stage_stops_nothing_once_its_new_questions_are_answered()
    {
        var scenario = new Scenario { Stage = 1 };
        var decisions = new List<Decision> { Answer("a", 0), Answer("b", 0), Answer("c", 1) };

        var progress = Ledger.Progress(Grown, scenario, decisions);

        Assert.Empty(progress.Open);
        Assert.True(progress.Gate.Passes);
    }
}
