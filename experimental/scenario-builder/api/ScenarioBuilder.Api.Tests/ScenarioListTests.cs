// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class ScenarioListTests
{
    private static Task<HttpResponseMessage> Archive(HttpClient client, int id, bool archived) =>
        client.PutAsJsonAsync($"/api/scenarios/{id}/archive", new { archived });

    private static async Task<JsonObject> Listed(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<JsonArray>("/api/scenarios"))!.Single(s => (int)s!["id"]! == id)!.AsObject();

    [Fact]
    public async Task The_list_carries_when_each_was_created_and_whether_it_is_archived()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        var listed = await Listed(owner, id);

        Assert.False((bool)listed["archived"]!);
        Assert.True(DateTime.Parse((string)listed["created"]!) > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Only_the_owner_as_a_person_archives_a_scenario()
    {
        using var factory = new ApiFactory();
        var owner = factory.Client();
        var id = await CreateScenario(owner);

        Assert.Equal("NOT_OWNER", await Code(await Archive(factory.Client(user: "lee"), id, true)));
        var byAgent = await Archive(factory.Client(agent: true), id, true);
        Assert.Equal(HttpStatusCode.Forbidden, byAgent.StatusCode);
        Assert.Equal("AGENT_CANNOT_DECIDE", await Code(byAgent));
        Assert.False((bool)(await Listed(owner, id))["archived"]!);

        Assert.Equal(HttpStatusCode.OK, (await Archive(owner, id, true)).StatusCode);
        Assert.True((bool)(await Listed(owner, id))["archived"]!);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/scenarios/{id}")).StatusCode);

        await Archive(owner, id, false);
        Assert.False((bool)(await Listed(owner, id))["archived"]!);
    }
}
