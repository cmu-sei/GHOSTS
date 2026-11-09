// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class GateTests
{
    [Fact]
    public async Task Stage0_passes_once_its_questions_are_answered()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await AnswerStage(owner, id, 0);

        var gate = (await Progress(owner, id))["gate"]!.AsObject();
        Assert.Equal(["passes", "findings"], gate.Select(p => p.Key));
        Assert.True((bool)gate["passes"]!);
        var advanced = await Advance(factory.Client(agent: true), id);
        Assert.Equal(1, (int)(await Body(advanced))["stage"]!);
    }

    [Fact]
    public async Task Advance_with_questions_open_is_409()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var advance = await Advance(owner, id);

        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        var error = await Body(advance);
        Assert.Equal("GATE_CLOSED", (string)error["code"]!);
        Assert.Contains("'participants' is not answered", (string)error["message"]!);
    }

    [Fact]
    public async Task Current_stage_findings_carry_their_stage()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var findings = (await Progress(owner, id))["gate"]!["findings"]!.AsArray();

        Assert.All(findings, f => Assert.Equal(0, (int)f!["stage"]!));
    }

    [Fact]
    public async Task Summary_not_due_until_stage2_answered()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 2);
        await Put(owner, id, "problem", new { value = "Payments fail" });

        var early = await Draft(factory.Client(agent: true), id, "Too soon.");
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal("DRAFT_NOT_DUE", await Code(early));

        await AnswerStage(owner, id, 2);
        var progress = await Progress(owner, id);
        Assert.Equal(["executiveSummary"], progress["draftsDue"]!.AsArray().Select(d => (string)d!));
        Assert.Contains("DRAFT_MISSING", await FindingCodes(owner, id));

        Assert.Equal(HttpStatusCode.OK, (await Draft(factory.Client(agent: true), id, "The summary.")).StatusCode);
        Assert.Empty((await Progress(owner, id))["draftsDue"]!.AsArray());
        Assert.Empty(await FindingCodes(owner, id));
    }

    [Fact]
    public async Task Unknown_draft_and_blank_text_are_rejected()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        Assert.Equal("DRAFT_UNKNOWN", await Code(await Draft(owner, id, "x", field: "nope")));
        Assert.Equal("TEXT_REQUIRED", await Code(await Draft(owner, id, " ")));
    }

    [Fact]
    public async Task Last_stage_advance_is_409()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        var last = ScenarioTypeOf(TabletopType).LastStage;
        await ReachStage(owner, id, last);
        await AnswerStage(owner, id, last);

        var progress = await Progress(owner, id);
        Assert.True((bool)progress["lastStage"]!);
        Assert.True((bool)progress["gate"]!["passes"]!);
        var advance = await Advance(owner, id);
        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        Assert.Equal("LAST_STAGE", await Code(advance));
    }

    [Fact]
    public async Task Log_lists_answers_drafts_proposals_in_order()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 2);
        await AnswerStage(owner, id, 2);
        var proposal = await factory.Client(agent: true).PostAsJsonAsync($"/api/scenarios/{id}/proposals",
            new { questionId = "problem", value = "Payments fail on and off", reason = "Sharper" });
        await owner.PostAsync($"/api/scenarios/{id}/proposals/{(int)(await Body(proposal))["id"]!}/apply", null);
        await Draft(factory.Client(agent: true), id, "The summary.");

        var log = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/log"))!;

        var types = log.Select(e => (string)e!["type"]!).ToList();
        Assert.Equal(["answer", "draft", "proposal"], types.Distinct().Order());
        Assert.Equal("draft", types[^1]);
        var times = log.Select(e => (DateTime)e!["time"]!).ToList();
        Assert.Equal(times.Order(), times);
        var draft = log.Single(e => (string)e!["type"]! == "draft")!;
        Assert.Equal("executiveSummary", (string)draft["target"]!);
        Assert.Equal("agent", (string)draft["via"]!);
    }
}
