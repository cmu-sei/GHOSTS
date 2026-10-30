// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class ProposalTests
{
    private static Task<HttpResponseMessage> Propose(HttpClient client, int id, string questionId, object value, string reason = "Both attend") =>
        client.PostAsJsonAsync($"/api/scenarios/{id}/proposals", new { questionId, value, reason });

    private static Task<HttpResponseMessage> Decide(HttpClient client, int id, int proposalId, string action) =>
        client.PostAsync($"/api/scenarios/{id}/proposals/{proposalId}/{action}", null);

    /// <summary>A scenario whose audienceType the owner stated as Civilian, and the agent's pending proposal to change it.</summary>
    private static async Task<(int Id, int ProposalId)> PendingProposal(ApiFactory factory)
    {
        var id = await CreateScenario(factory.Client());
        await Put(factory.Client(), id, "audienceType", new { value = "Civilian" });
        var proposal = await Propose(factory.Client(agent: true), id, "audienceType", "Mixed");
        Assert.Equal(HttpStatusCode.Created, proposal.StatusCode);
        return (id, (int)(await Body(proposal))["id"]!);
    }

    [Fact]
    public async Task Agent_proposes_owner_applies()
    {
        using var factory = new ApiFactory();
        var (id, proposalId) = await PendingProposal(factory);

        var applied = await Decide(factory.Client(), id, proposalId, "apply");

        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var answer = (await Progress(factory.Client(), id))["answered"]!.AsArray().Single(a => (string)a!["id"]! == "audienceType")!;
        Assert.Equal("Mixed", (string)answer["value"]!);
        Assert.Equal("stated", (string)answer["who"]!);
        Assert.Equal("person", (string)answer["via"]!);
        Assert.Equal("Both attend", (string)answer["reason"]!);
        var all = await factory.Client().GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/proposals");
        Assert.Equal("applied", (string)all![0]!["status"]!);
        var pending = await factory.Client().GetFromJsonAsync<JsonArray>($"/api/scenarios/{id}/proposals?status=pending");
        Assert.Empty(pending!);
    }

    [Fact]
    public async Task Owner_discards_and_the_answer_stands()
    {
        using var factory = new ApiFactory();
        var (id, proposalId) = await PendingProposal(factory);

        await Decide(factory.Client(), id, proposalId, "discard");

        var answer = (await Progress(factory.Client(), id))["answered"]!.AsArray().Single(a => (string)a!["id"]! == "audienceType")!;
        Assert.Equal("Civilian", (string)answer["value"]!);
    }

    [Fact]
    public async Task Agent_cannot_apply()
    {
        using var factory = new ApiFactory();
        var (id, proposalId) = await PendingProposal(factory);

        var response = await Decide(factory.Client(agent: true), id, proposalId, "apply");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("AGENT_CANNOT_DECIDE", await Code(response));
    }

    [Fact]
    public async Task Non_owner_cannot_apply()
    {
        using var factory = new ApiFactory();
        var (id, proposalId) = await PendingProposal(factory);

        var response = await Decide(factory.Client(user: "lee"), id, proposalId, "apply");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("NOT_OWNER", await Code(response));
    }

    [Fact]
    public async Task Proposal_on_unstated_question_is_409()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Propose(factory.Client(agent: true), id, "audienceType", "Mixed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await Body(response);
        Assert.Equal("NOT_STATED", (string)error["code"]!);
        Assert.Contains("record_answer", (string)error["message"]!);
    }

    [Fact]
    public async Task Proposal_needs_a_valid_value_and_a_reason()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());
        await Put(factory.Client(), id, "audienceType", new { value = "Civilian" });

        Assert.Equal("VALUE_INVALID", await Code(await Propose(factory.Client(agent: true), id, "audienceType", "Police")));
        Assert.Equal("REASON_REQUIRED", await Code(await Propose(factory.Client(agent: true), id, "audienceType", "Mixed", reason: " ")));
    }

    [Fact]
    public async Task Applying_twice_is_409()
    {
        using var factory = new ApiFactory();
        var (id, proposalId) = await PendingProposal(factory);
        await Decide(factory.Client(), id, proposalId, "apply");

        var again = await Decide(factory.Client(), id, proposalId, "apply");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("NOT_PENDING", await Code(again));
    }

    [Fact]
    public async Task Proposal_is_stale_once_the_answer_changes()
    {
        using var factory = new ApiFactory();
        var (id, proposalId) = await PendingProposal(factory);
        await Put(factory.Client(), id, "audienceType", new { value = "Military" });

        var response = await Decide(factory.Client(), id, proposalId, "apply");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("STALE_PROPOSAL", await Code(response));
        var answer = (await Progress(factory.Client(), id))["answered"]!.AsArray().Single(a => (string)a!["id"]! == "audienceType")!;
        Assert.Equal("Military", (string)answer["value"]!);
    }
}
