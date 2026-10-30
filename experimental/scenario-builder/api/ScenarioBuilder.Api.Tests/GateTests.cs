// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class GateTests
{
    [Fact]
    public async Task Stage0_needs_answers_then_approval()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await AnswerStage(owner, id, 0);

        Assert.Equal(["APPROVAL_MISSING"], await FindingCodes(owner, id));
        var byAgent = await Approve(factory.Client(agent: true), id, 0);
        Assert.Equal(HttpStatusCode.Forbidden, byAgent.StatusCode);
        Assert.Equal("AGENT_CANNOT_DECIDE", await Code(byAgent));
        Assert.Equal("NOT_OWNER", await Code(await Approve(factory.Client(user: "lee"), id, 0)));

        Assert.Equal(HttpStatusCode.OK, (await Approve(owner, id, 0)).StatusCode);
        Assert.True((bool)(await Progress(owner, id))["gate"]!["passes"]!);
        var advanced = await Advance(factory.Client(agent: true), id);
        Assert.Equal(1, (int)(await Body(advanced))["stage"]!);
    }

    [Fact]
    public async Task Approval_before_answers_is_not_ready()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var response = await Approve(owner, id, 0);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await Body(response);
        Assert.Equal("GATE_NOT_READY", (string)error["code"]!);
        Assert.Contains("'participants' is not answered", (string)error["message"]!);
    }

    [Fact]
    public async Task Edit_after_approval_makes_it_stale()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await AnswerStage(owner, id, 0);
        await Approve(owner, id, 0);

        await Put(owner, id, "recording", new { value = "Nothing is recorded" });

        Assert.Equal(["APPROVAL_STALE"], await FindingCodes(owner, id));
        var advance = await Advance(owner, id);
        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        var error = await Body(advance);
        Assert.Equal("GATE_CLOSED", (string)error["code"]!);
        Assert.Contains("approve again", (string)error["message"]!);
    }

    [Fact]
    public async Task Stage1_has_no_approval()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 1);

        await AnswerStage(owner, id, 1);

        Assert.True((bool)(await Progress(owner, id))["gate"]!["passes"]!);
        Assert.Equal("NO_APPROVAL_NEEDED", await Code(await Approve(owner, id, 1)));
    }

    [Fact]
    public async Task Approving_a_stage_that_is_not_open_is_409()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        Assert.Equal("STAGE_NOT_OPEN", await Code(await Approve(owner, id, 2)));
    }

    [Fact]
    public async Task Approving_a_stage_the_kind_does_not_have_is_404()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var response = await Approve(owner, id, 99);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("STAGE_UNKNOWN", await Code(response));
    }

    [Fact]
    public async Task Closed_stage_edit_after_approval_reopens_its_approval()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 1);
        await AnswerStage(owner, id, 1);

        await Put(owner, id, "recording", new { value = "Changed after stage 0 was approved" });

        var findings = (await Progress(owner, id))["gate"]!["findings"]!.AsArray();
        var stale = Assert.Single(findings);
        Assert.Equal("APPROVAL_STALE", (string)stale!["code"]!);
        Assert.Equal(0, (int)stale["stage"]!);
        Assert.Contains("Stage 0", (string)stale["message"]!);
        Assert.Equal("GATE_CLOSED", await Code(await Advance(owner, id)));

        Assert.Equal(HttpStatusCode.OK, (await Approve(owner, id, 0)).StatusCode);
        Assert.True((bool)(await Progress(owner, id))["gate"]!["passes"]!);
        Assert.Equal(2, (int)(await Body(await Advance(owner, id)))["stage"]!);
    }

    [Fact]
    public async Task Closed_stage_staleness_shows_in_the_pulled_document_on_the_last_stage()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 3);
        await AnswerStage(owner, id, 3);
        await Approve(owner, id, 3);

        await Draft(factory.Client(agent: true), id, "A summary rewritten after its approval.");

        var doc = (await owner.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!;
        var finding = Assert.Single(doc["findings"]!.AsArray());
        Assert.Equal("APPROVAL_STALE", (string)finding!["code"]!);
        Assert.Equal(2, (int)finding["stage"]!);
        Assert.False((bool)(await Progress(owner, id))["gate"]!["passes"]!);
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
        Assert.Equal(["APPROVAL_MISSING"], await FindingCodes(owner, id));
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
    public async Task Stage2_approval_covers_the_summary()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await ReachStage(owner, id, 2);
        await AnswerStage(owner, id, 2);
        await Draft(owner, id, "The summary.");
        await Approve(owner, id, 2);

        await Draft(factory.Client(agent: true), id, "A rewritten summary.");

        Assert.Equal(["APPROVAL_STALE"], await FindingCodes(owner, id));
    }

    [Fact]
    public async Task Last_stage_advance_is_409()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        var last = KindOf(Kind).LastStage;
        await ReachStage(owner, id, last);
        await AnswerStage(owner, id, last);
        await Approve(owner, id, last);

        var progress = await Progress(owner, id);
        Assert.True((bool)progress["lastStage"]!);
        Assert.True((bool)progress["gate"]!["passes"]!);
        var advance = await Advance(owner, id);
        Assert.Equal(HttpStatusCode.Conflict, advance.StatusCode);
        Assert.Equal("LAST_STAGE", await Code(advance));
    }

    [Fact]
    public async Task Log_lists_answers_drafts_proposals_approvals_in_order()
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
        await Approve(owner, id, 2);

        var log = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/log"))!;

        var types = log.Select(e => (string)e!["type"]!).ToList();
        Assert.Equal(["answer", "approval", "draft", "proposal"], types.Distinct().Order());
        Assert.Equal("approval", types[^1]);
        var times = log.Select(e => (DateTime)e!["time"]!).ToList();
        Assert.Equal(times.Order(), times);
        var draft = log.Single(e => (string)e!["type"]! == "draft")!;
        Assert.Equal("executiveSummary", (string)draft["target"]!);
        Assert.Equal("agent", (string)draft["via"]!);
    }
}
