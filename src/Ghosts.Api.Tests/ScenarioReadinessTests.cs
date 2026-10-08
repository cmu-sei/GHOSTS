// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.ScenarioDocuments;

namespace Ghosts.Api.Tests;

/// <summary>
/// The readiness dashboard beside the conversation: the fourteen questions read from the document and the
/// ledger in the agent's reply, and the findings sorted the way the prompt sorts them.
/// </summary>
public class ScenarioReadinessTests
{
    [Fact]
    public void Before_the_first_draft_every_question_is_open()
    {
        var readiness = ScenarioReadiness.Compute(null, [], null);

        Assert.Equal(14, readiness.Questions.Count);
        Assert.All(readiness.Questions, q => Assert.Equal(ScenarioReadiness.Open, q.Status));
        Assert.Equal(0, readiness.Answered);
        Assert.Equal(14, readiness.Open);
        Assert.Equal(new ReadinessLedger(0, 0, 14), readiness.Ledger);
        Assert.All(readiness.Coverage, c => Assert.Equal(0, c.Count));
        Assert.Null(readiness.Clock.Duration);
    }

    [Fact]
    public void The_document_answers_a_question_as_drafted_and_leaves_the_rest_open()
    {
        var doc = Example("meridian-hybrid");

        var readiness = ScenarioReadiness.Compute(doc, [], null);
        var byTag = readiness.Questions.ToDictionary(q => q.Tag);

        Assert.Equal(ScenarioReadiness.Drafted, byTag["A1"].Status);
        Assert.StartsWith("Incident Commander", byTag["A1"].Value);
        Assert.Equal(ScenarioReadiness.Drafted, byTag["D1"].Status);
        Assert.Equal(ScenarioReadiness.Drafted, byTag["E1"].Status);
        Assert.Equal("1h30m", byTag["E1"].Value);
        Assert.Equal(ScenarioReadiness.Drafted, byTag["E3"].Status);
        Assert.Contains("Cinder Wolf", byTag["E3"].Value);
        Assert.Equal(ScenarioReadiness.Drafted, byTag["B2"].Status);  // objectives with metWhen
        Assert.Equal(ScenarioReadiness.Open, byTag["A3"].Status);     // no population.pools
        Assert.Equal(ScenarioReadiness.Open, byTag["C2"].Status);     // no branching
        Assert.Equal(ScenarioReadiness.Drafted, byTag["E2"].Status);  // four hosts, no slice
        Assert.Equal("0 segments; 4 hosts", byTag["E2"].Value);
        Assert.Equal(readiness.Questions.Count - readiness.Open, readiness.Answered);

        Assert.Equal(2, readiness.Coverage.Single(c => c.Label == "events").Count);
        Assert.Equal(4, readiness.Coverage.Single(c => c.Label == "hosts").Count);
        Assert.Equal("1h30m", readiness.Clock.Duration);
        Assert.Equal("2, 0 at a fixed time", readiness.Clock.Events);
    }

    [Fact]
    public void The_ledger_in_the_reply_says_who_decided_and_its_value_wins()
    {
        var doc = Example("meridian-hybrid");
        const string reply = """
            Here is where we are.

            **Proposed by me (2)**

            | # | Decision | Value | Who | Why |
            |---|---|---|---|---|
            | 1 | C1 last recoverable rung | rung 3, credential reuse contained | proposed | Past rung 4 the trip is scheduled. |
            | 3 | slug and name | meridian-hybrid | proposed | From the intent. |

            **Stated by you (1)**

            | # | Decision | Value | Who | Why |
            |---|---|---|---|---|
            | 2 | D1 rules of engagement | no live malware; isolation permitted | stated | — |
            """;

        Assert.True(ScenarioReadiness.HasLedger(reply));
        Assert.False(ScenarioReadiness.HasLedger("No table here."));

        var readiness = ScenarioReadiness.Compute(doc, [], reply);
        var byTag = readiness.Questions.ToDictionary(q => q.Tag);

        Assert.Equal(ScenarioReadiness.Proposed, byTag["C1"].Status);
        Assert.Equal("rung 3, credential reuse contained", byTag["C1"].Value);
        Assert.Equal(ScenarioReadiness.Stated, byTag["D1"].Status);
        Assert.Equal("no live malware; isolation permitted", byTag["D1"].Value);
        // A row that answers none of the fourteen is counted, bound to nothing
        Assert.Equal(1, readiness.Ledger.Stated);
        Assert.Equal(2, readiness.Ledger.Proposed);
        Assert.Equal(readiness.Open, readiness.Ledger.Open);
    }

    [Fact]
    public void Findings_sort_into_the_prompts_three_piles_errors_first()
    {
        List<ScenarioFinding> findings =
        [
            ScenarioFinding.Note(2, "TERRAIN_UNSTRUCTURED", "/terrain", "Terrain is prose."),
            ScenarioFinding.Warn(2, "FLAG_NEVER_READ", "/timeline/events/1/effects", "Nothing reads it."),
            ScenarioFinding.Warn(4, "DRYRUN_NPCS", "/population/pools", "No pools."),
            ScenarioFinding.Note(4, "DRYRUN_NPCS", "/population/pools/0", "12 NPCs generated."),
            ScenarioFinding.Err(2, "TIME_OUTSIDE_DURATION", "/timeline/events/5/at", "After the end."),
        ];

        var sorted = ScenarioReadiness.Compute(null, findings, null).Findings;

        Assert.Equal(1, sorted.Errors);
        Assert.Equal(2, sorted.Warnings);
        Assert.Equal(2, sorted.Info);
        Assert.Equal("TIME_OUTSIDE_DURATION", sorted.Items[0].Code);
        Assert.Equal(("judgment", "E1 or B3"), (sorted.Items[0].Kind, sorted.Items[0].Asks));
        Assert.Equal(("mechanical", (string)null), Pile(sorted, "FLAG_NEVER_READ"));
        Assert.Equal(("judgment", "A3"), Pile(sorted, "DRYRUN_NPCS", ScenarioFinding.Warning));
        Assert.Equal(("accepted", (string)null), Pile(sorted, "DRYRUN_NPCS", ScenarioFinding.Info));
        Assert.Equal(("judgment", "E2"), Pile(sorted, "TERRAIN_UNSTRUCTURED"));
    }

    private static (string, string) Pile(ReadinessFindings findings, string code, string severity = null)
    {
        var f = findings.Items.Single(i => i.Code == code && (severity == null || i.Severity == severity));
        return (f.Kind, f.Asks);
    }

    private static JsonObject Example(string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Examples, $"{name}.scenario.json")))!.AsObject();

    private static readonly string Examples = Find();

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "schemas", "scenario-document", "examples");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("schemas/scenario-document/examples is not above the test binary");
    }
}
