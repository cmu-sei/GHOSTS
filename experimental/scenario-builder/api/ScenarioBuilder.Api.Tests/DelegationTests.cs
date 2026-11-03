// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class DelegationTests
{
    private static Task<HttpResponseMessage> Delegate(HttpClient client, int id, bool on) =>
        client.PutAsJsonAsync($"/api/scenarios/{id}/delegation", new { on });

    [Fact]
    public async Task Only_the_owner_as_a_person_can_hand_over_the_decisions()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var byAgent = await Delegate(factory.Client(agent: true), id, true);
        Assert.Equal(HttpStatusCode.Forbidden, byAgent.StatusCode);
        Assert.Equal("AGENT_CANNOT_DECIDE", await Code(byAgent));
        Assert.Equal("NOT_OWNER", await Code(await Delegate(factory.Client(user: "lee"), id, true)));
        Assert.False((bool)(await Progress(owner, id))["delegated"]!);

        Assert.Equal(HttpStatusCode.OK, (await Delegate(owner, id, true)).StatusCode);
        Assert.True((bool)(await Progress(owner, id))["delegated"]!);

        await Delegate(owner, id, false);
        Assert.False((bool)(await Progress(owner, id))["delegated"]!);
    }

    [Fact]
    public async Task The_agent_approves_a_gate_only_while_delegated()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var agent = factory.Client(agent: true);
        var id = await CreateScenario(owner);
        await AnswerStage(owner, id, 0);

        var before = await Approve(agent, id, 0);
        Assert.Equal(HttpStatusCode.Forbidden, before.StatusCode);
        Assert.Contains("hands you the decisions", (string)(await Body(before))["message"]!);

        await Delegate(owner, id, true);
        Assert.Equal(HttpStatusCode.OK, (await Approve(agent, id, 0)).StatusCode);
        Assert.True((bool)(await Progress(owner, id))["gate"]!["passes"]!);

        await Delegate(owner, id, false);
        await Put(owner, id, "recording", new { value = "Changed" });
        Assert.Equal("AGENT_CANNOT_DECIDE", await Code(await Approve(agent, id, 0)));
    }

    [Fact]
    public async Task The_log_shows_the_handover_and_approvals_made_through_the_agent()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await AnswerStage(owner, id, 0);
        await Delegate(owner, id, true);
        await Approve(factory.Client(agent: true), id, 0);

        var log = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/log"))!;

        var handover = log.Single(e => (string)e!["type"]! == "delegation")!;
        Assert.Equal("person", (string)handover["via"]!);
        Assert.Equal("on", (string)handover["after"]!);
        var approval = log.Single(e => (string)e!["type"]! == "approval")!;
        Assert.Equal("agent", (string)approval["via"]!);
        Assert.Equal("dev", (string)approval["by"]!);
    }
}
