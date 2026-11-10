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
    public async Task The_log_shows_the_handover()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);
        await Delegate(owner, id, true);

        var log = (await owner.GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/log"))!;

        var handover = log.Single(e => (string)e!["type"]! == "delegation")!;
        Assert.Equal("person", (string)handover["via"]!);
        Assert.Equal("on", (string)handover["after"]!);
    }
}
