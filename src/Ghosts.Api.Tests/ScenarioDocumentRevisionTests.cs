// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.ScenarioDocuments;

namespace Ghosts.Api.Tests;

/// <summary>
/// What a revision is made of: the JSON Patch the agent sends (D1), the change list the server computes
/// from the two versions (D2), and the report of what the exercise still lacks (E5).
/// </summary>
public class ScenarioDocumentRevisionTests
{
    // ───────── the patch ─────────

    [Fact]
    public void Each_operation_applies_and_the_document_given_is_not_changed()
    {
        var document = JsonNode.Parse("""{"name":"a","tags":["x","y"],"rules":{"pacing":"real-time"}}""")!;

        var patched = ScenarioDocumentPatch.Apply(document, Ops(
            """{"op":"replace","path":"/name","value":"b"}""",
            """{"op":"add","path":"/tags/-","value":"z"}""",
            """{"op":"add","path":"/tags/0","value":"w"}""",
            """{"op":"remove","path":"/tags/1"}""",
            """{"op":"copy","from":"/name","path":"/slug"}""",
            """{"op":"move","from":"/rules/pacing","path":"/pacing"}""",
            """{"op":"test","path":"/pacing","value":"real-time"}"""));

        Assert.Equal("""{"name":"b","tags":["w","y","z"],"rules":{},"slug":"b","pacing":"real-time"}""", patched!.ToJsonString());
        Assert.Equal("""{"name":"a","tags":["x","y"],"rules":{"pacing":"real-time"}}""", document.ToJsonString());
    }

    [Fact]
    public void Replace_keeps_the_key_where_it_was()
    {
        var patched = ScenarioDocumentPatch.Apply(JsonNode.Parse("""{"a":1,"b":2,"c":3}"""), Ops("""{"op":"replace","path":"/b","value":9}"""));

        Assert.Equal("""{"a":1,"b":9,"c":3}""", patched!.ToJsonString());
    }

    [Theory]
    [InlineData("""{"op":"replace","path":"/missing","value":1}""", "nothing is at /missing")]
    [InlineData("""{"op":"remove","path":"/a/5"}""", "nothing is at /a/5")]
    [InlineData("""{"op":"add","path":"a","value":1}""", "is not a JSON Pointer")]
    [InlineData("""{"op":"test","path":"/b","value":"other"}""", "not the one the test expects")]
    [InlineData("""{"op":"rename","path":"/b"}""", "is not a JSON Patch operation")]
    [InlineData("""{"op":"add","path":"/c"}""", "it has no value")]
    public void An_operation_that_cannot_apply_says_which_and_why(string op, string reason)
    {
        var ex = Assert.Throws<ScenarioPatchException>(() =>
            ScenarioDocumentPatch.Apply(JsonNode.Parse("""{"a":[1],"b":"x"}"""), Ops(op)));

        Assert.StartsWith("Operation 0 (", ex.Message);
        Assert.Contains(reason, ex.Message);
    }

    [Fact]
    public void Pointer_escapes_are_read()
    {
        var patched = ScenarioDocumentPatch.Apply(JsonNode.Parse("""{"a/b":1,"c~d":2}"""), Ops(
            """{"op":"replace","path":"/a~1b","value":3}""",
            """{"op":"remove","path":"/c~0d"}"""));

        Assert.Equal("""{"a/b":3}""", patched!.ToJsonString());
    }

    // ───────── the change list ─────────

    [Fact]
    public void The_change_list_names_each_changed_pointer_with_its_old_and_new_value()
    {
        var before = JsonNode.Parse("""{"timeline":{"events":[{"at":"T+5h30m"},{"at":"T+7h"}]},"name":"a","old":true}""");
        var after = JsonNode.Parse("""{"timeline":{"events":[{"at":"T+6h"}]},"name":"a","new":{"k":1}}""");

        Assert.Equal(
        [
            "/timeline/events/0/at: T+5h30m → T+6h",
            """/timeline/events/1: {"at":"T+7h"} → (removed)""",
            """/new: (none) → {"k":1}""",
            "/old: true → (removed)"
        ], ScenarioDocumentDiff.Changes(before, after));
    }

    [Fact]
    public void The_same_document_in_another_key_order_has_no_changes()
    {
        Assert.Empty(ScenarioDocumentDiff.Changes(JsonNode.Parse("""{"a":1,"b":[2]}"""), JsonNode.Parse("""{"b":[2],"a":1}""")));
    }

    [Fact]
    public void A_long_value_is_cut_and_a_long_list_is_capped()
    {
        var line = Assert.Single(ScenarioDocumentDiff.Changes(JsonNode.Parse("""{"d":"x"}"""), new JsonObject { ["d"] = new string('y', 200) }));
        Assert.EndsWith(new string('y', 80) + "…", line);

        var many = new JsonObject();
        for (var i = 0; i < 250; i++) many[$"k{i}"] = i;
        var changes = ScenarioDocumentDiff.Changes(new JsonObject(), many);
        Assert.Equal(ScenarioDocumentDiff.MaxLines + 1, changes.Count);
        Assert.Equal("… and 50 more changes", changes[^1]);
    }

    // ───────── what the exercise still lacks ─────────

    [Fact]
    public void An_empty_document_lacks_all_four()
    {
        Assert.Equal(4, ScenarioCompleteness.Gaps(new JsonObject()).Count);
    }

    [Fact]
    public void A_complete_document_lacks_nothing_and_a_blank_value_counts_as_missing()
    {
        var complete = JsonNode.Parse("""
            {"audience":{"role":"SOC analyst","rulesOfEngagement":"No live malware."},
             "rulesOfPlay":{"pacing":"real-time"},
             "assessment":{"objectives":[{"id":1,"name":"Contain","metWhen":{"flag":"contained"}}]}}
            """)!.AsObject();
        Assert.Empty(ScenarioCompleteness.Gaps(complete));

        complete["audience"]!["rulesOfEngagement"] = "  ";
        Assert.Equal(["No rules of engagement (audience.rulesOfEngagement)."], ScenarioCompleteness.Gaps(complete));
    }

    private static JsonArray Ops(params string[] ops) => new(ops.Select(o => JsonNode.Parse(o)).ToArray());
}
