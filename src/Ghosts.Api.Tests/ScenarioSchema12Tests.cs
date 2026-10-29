// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.ScenarioDocuments;
using Ghosts.Api.Infrastructure.Services;

namespace Ghosts.Api.Tests;

/// <summary>
/// Schema 1.2.0 adds what running an exercise needs: the terrain reference names an enclave and its base, an event
/// can say what an NPC does and which NPCs do it, a pool can say where its people sit, and a decision point can
/// list its choices. A 1.1.0 document still reads, and its "slice" is read as the enclave.
/// </summary>
public class ScenarioSchema12Tests
{
    private const string Office = """
        {
          "schemaVersion": "1.2.0", "slug": "office-day", "name": "Office Day", "description": "An ordinary day at the office.",
          "terrain": {
            "reference": { "provider": "grayspace", "enclave": "office-enclave", "base": "office-base" },
            "segments": [ { "name": "Office LAN", "cidr": "10.20.0.0/24" } ],
            "hosts": [ { "name": "desk-01", "segment": "Office LAN" }, { "name": "desk-02", "segment": "Office LAN" } ]
          },
          "population": { "pools": [ { "role": "office staff", "count": 2, "segments": ["Office LAN"], "hosts": ["desk-01", "desk-02"] } ] },
          "timeline": { "events": [
            { "id": "check-email", "at": "T+5m", "owner": "green-cell", "description": "Staff read the morning newsletter",
              "execution": { "mode": "npc",
                             "action": { "handler": "BrowserChrome", "commands": ["browse"], "commandArgs": ["http://intranet.office.local/news"] },
                             "targets": { "pools": ["office staff"] } } },
            { "id": "lunch-plan", "at": "T+1h", "owner": "white-cell", "description": "Pick where the team has lunch",
              "choices": [ { "id": "cafe", "label": "The cafe", "effects": { "setFlags": ["lunch-at-cafe"] } },
                           { "id": "kitchen", "label": "The office kitchen", "effects": { "setFlags": ["lunch-in-kitchen"] } } ] },
            { "id": "after-lunch", "at": "T+2h", "owner": "white-cell", "description": "Coffee after lunch", "when": "flag:lunch-at-cafe" }
          ] }
        }
        """;

    // ── the schema ───────────────────────────────────────────────────────

    [Fact]
    public void A_1_2_0_document_with_an_enclave_npc_actions_pool_placement_and_choices_is_valid()
    {
        var result = ScenarioDocumentValidator.Validate(Parse(Office));

        Assert.True(result.IsValid, string.Join("\n", result.Findings.Select(f => $"{f.Code} {f.Path} {f.Message}")));
        Assert.DoesNotContain(result.Findings, f => f.Code == "REF_UNSET_FLAG");
    }

    [Fact]
    public void A_1_1_0_document_naming_a_slice_is_still_valid()
    {
        var doc = Parse(Office);
        doc["schemaVersion"] = "1.1.0";
        doc["terrain"]!["reference"] = JsonNode.Parse("""{ "provider": "grayspace", "slice": "office-enclave" }""");

        Assert.True(ScenarioDocumentValidator.Validate(doc).IsValid);
    }

    [Fact]
    public void A_terrain_reference_with_no_enclave_is_refused()
    {
        var doc = Parse(Office);
        doc["terrain"]!["reference"] = JsonNode.Parse("""{ "provider": "grayspace", "base": "office-base" }""");

        Assert.False(ScenarioDocumentValidator.Validate(doc).IsValid);
    }

    [Fact]
    public void An_npc_action_must_name_an_everyday_handler()
    {
        var doc = Parse(Office);
        doc["timeline"]!["events"]![0]!["execution"]!["action"]!["handler"] = "NoSuchHandler";

        Assert.False(ScenarioDocumentValidator.Validate(doc).IsValid);
    }

    [Theory]
    [InlineData("BrowserChrome")]
    [InlineData("BrowserFirefox")]
    [InlineData("Word")]
    [InlineData("Excel")]
    [InlineData("PowerPoint")]
    [InlineData("Print")]
    public void An_npc_action_whose_handler_needs_arguments_must_give_them(string handler)
    {
        var doc = Parse(Office);
        doc["timeline"]!["events"]![0]!["execution"]!["action"] = JsonNode.Parse($$"""{ "handler": "{{handler}}" }""");

        Assert.False(ScenarioDocumentValidator.Validate(doc).IsValid);
    }

    [Theory]
    [InlineData("Notepad")]
    [InlineData("Outlook")]
    public void Notepad_and_outlook_actions_need_no_arguments(string handler)
    {
        var doc = Parse(Office);
        doc["timeline"]!["events"]![0]!["execution"]!["action"] = JsonNode.Parse($$"""{ "handler": "{{handler}}" }""");

        var result = ScenarioDocumentValidator.Validate(doc);

        Assert.True(result.IsValid, string.Join("\n", result.Findings.Select(f => $"{f.Code} {f.Path} {f.Message}")));
    }

    [Fact]
    public void A_target_pool_that_the_document_does_not_declare_is_an_error_at_its_pointer()
    {
        var doc = Parse(Office);
        doc["timeline"]!["events"]![0]!["execution"]!["targets"]!["pools"]![0] = "night shift";

        var finding = Assert.Single(ScenarioDocumentValidator.Validate(doc).Findings, f => f.Code == "REF_UNKNOWN_POOL");

        Assert.Equal("/timeline/events/0/execution/targets/pools/0", finding.Path);
        Assert.Equal(ScenarioFinding.Error, finding.Severity);
    }

    [Fact]
    public void A_pool_placed_on_an_undeclared_segment_is_an_error_at_its_pointer()
    {
        var doc = Parse(Office);
        doc["population"]!["pools"]![0]!["segments"]![0] = "Basement";

        var finding = Assert.Single(ScenarioDocumentValidator.Validate(doc).Findings, f => f.Code == "REF_UNKNOWN_SEGMENT");

        Assert.Equal("/population/pools/0/segments/0", finding.Path);
    }

    [Fact]
    public void A_target_host_that_the_terrain_does_not_declare_is_an_error_at_its_pointer()
    {
        var doc = Parse(Office);
        doc["timeline"]!["events"]![0]!["execution"]!["targets"] = JsonNode.Parse("""{ "hosts": ["desk-99"] }""");

        var finding = Assert.Single(ScenarioDocumentValidator.Validate(doc).Findings, f => f.Code == "REF_UNKNOWN_HOST" && f.Path.Contains("targets"));

        Assert.Equal("/timeline/events/0/execution/targets/hosts/0", finding.Path);
    }

    // ── import and export ────────────────────────────────────────────────

    [Fact]
    public void Import_keeps_the_enclave_and_base_and_reads_a_slice_as_the_enclave()
    {
        var current = ScenarioDocumentMapper.FromDocument(Parse(Office)).Extras;
        var old = Parse(Office);
        old["schemaVersion"] = "1.1.0";
        old["terrain"]!["reference"] = JsonNode.Parse("""{ "provider": "grayspace", "slice": "office-enclave" }""");
        var upgraded = ScenarioDocumentMapper.FromDocument(old).Extras;

        Assert.Equal("office-enclave", current.Terrain.Reference.Enclave);
        Assert.Equal("office-base", current.Terrain.Reference.Base);
        Assert.Equal("office-enclave", upgraded.Terrain.Reference.Enclave);
        Assert.Null(upgraded.Terrain.Reference.Slice);
    }

    [Fact]
    public void Import_keeps_an_events_npc_action_targets_and_choices_and_a_pools_placement()
    {
        var dto = ScenarioDocumentMapper.FromDocument(Parse(Office));

        var first = dto.Timeline.Events[0];
        Assert.Equal("Npc", first.ExecutionType);
        Assert.Equal("BrowserChrome", first.Extras.Execution.Action.Handler);
        Assert.Equal(["browse"], first.Extras.Execution.Action.Commands);
        Assert.Equal(["http://intranet.office.local/news"], first.Extras.Execution.Action.CommandArgs);
        Assert.Equal(["office staff"], first.Extras.Execution.Targets.Pools);
        Assert.Equal(["cafe", "kitchen"], dto.Timeline.Events[1].Extras.Choices.Select(c => c.Id));
        var pool = Assert.Single(dto.ScenarioParameters.UserPools);
        Assert.Equal(["Office LAN"], pool.Extras.Segments);
        Assert.Equal(["desk-01", "desk-02"], pool.Extras.Hosts);
    }

    [Fact]
    public void An_npc_mode_item_is_dispatched_to_npcs_whoever_owns_it()
    {
        Assert.Equal("MachineUpdate", ExecutionService.AutomationKindFor(ExecutionType.Npc, "White Cell"));
        Assert.Equal("Manual", ExecutionService.AutomationKindFor(ExecutionType.Manual, "White Cell"));
        Assert.Equal("Workflow", ExecutionService.AutomationKindFor(ExecutionType.Workflow, "Green Cell"));
    }

    [Fact]
    public void Exported_documents_declare_1_2_0()
    {
        Assert.Equal("1.2.0", ScenarioDocumentMapper.SchemaVersion);
        Assert.Contains("1.1.0", ScenarioDocumentSchema.SupportedVersions);
        Assert.Contains("1.2.0", ScenarioDocumentSchema.SupportedVersions);
    }

    private static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();
}
