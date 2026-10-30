// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ScenarioBuilder.Api.Infrastructure;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class DecisionTests
{
    [Fact]
    public async Task Person_answer_is_stated_and_logged()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();
        var id = await CreateScenario(client);

        var response = await Put(client, id, "participants", new { value = "CERT leaders", who = "proposed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var answered = await Body(response);
        Assert.Equal("stated", (string)answered["who"]!);
        Assert.Equal("dev", (string)answered["by"]!);
        Assert.Equal("person", (string)answered["via"]!);
        Assert.Equal("CERT leaders", (string)answered["value"]!);
    }

    [Fact]
    public async Task Agent_cannot_overwrite_stated()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());
        await Put(factory.Client(), id, "participants", new { value = "CERT leaders" });

        var response = await Put(factory.Client(agent: true), id, "participants",
            new { value = "Analysts", who = "proposed", reason = "Seems likelier" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await Body(response);
        Assert.Equal("STATED", (string)error["code"]!);
        Assert.Contains("propose_change", (string)error["message"]!);
    }

    [Fact]
    public async Task Agent_stated_needs_quote()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(agent: true), id, "participants", new { value = "CERT leaders", who = "stated" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("QUOTE_REQUIRED", await Code(response));
    }

    [Fact]
    public async Task Agent_proposed_needs_reason()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(agent: true), id, "participants", new { value = "CERT leaders", who = "proposed" });

        Assert.Equal("REASON_REQUIRED", await Code(response));
    }

    [Fact]
    public async Task Agent_stated_with_quote_is_kept_as_stated()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(agent: true), id, "participants",
            new { value = "CERT leaders", who = "stated", quote = "for 25 CERT leaders" });

        var answered = await Body(response);
        Assert.Equal("stated", (string)answered["who"]!);
        Assert.Equal("agent", (string)answered["via"]!);
    }

    [Fact]
    public async Task Agent_may_overwrite_its_own_proposed()
    {
        using var factory = new ApiFactory();
        var agent = factory.Client(agent: true);
        var id = await CreateScenario(factory.Client());

        var first = await Put(agent, id, "complements", new { value = "None", who = "proposed", reason = "Nothing was named" });
        var second = await Put(agent, id, "complements", new { value = "The annual drill", who = "proposed", reason = "Better fit" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Future_stage_is_409()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(), id, "objectives", new { value = new[] { "Assess" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await Body(response);
        Assert.Equal("STAGE_NOT_OPEN", (string)error["code"]!);
        Assert.Contains("stage 1", (string)error["message"]!);
    }

    [Fact]
    public async Task Unknown_question_is_404()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(), id, "nope", new { value = "x" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("QUESTION_UNKNOWN", await Code(response));
    }

    [Fact]
    public async Task Choice_is_case_insensitive_and_stored_canonical()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(), id, "plainEnglish", new { value = "yes" });

        Assert.Equal("Yes", (string)(await Body(response))["value"]!);
    }

    [Fact]
    public async Task Choice_not_in_options_lists_them()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(), id, "audienceType", new { value = "Police" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await Body(response);
        Assert.Equal("VALUE_INVALID", (string)error["code"]!);
        Assert.Contains("Civilian, Military, Mixed", (string)error["message"]!);
    }

    [Fact]
    public async Task Empty_text_is_rejected()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());

        var response = await Put(factory.Client(), id, "participants", new { value = " " });

        Assert.Equal("VALUE_INVALID", await Code(response));
    }

    [Fact]
    public async Task List_as_string_is_rejected_with_example()
    {
        using var factory = new ApiFactory();
        var id = await CreateScenario(factory.Client());
        SetStage(factory, id, 1);

        var response = await Put(factory.Client(), id, "objectives", new { value = "Assess, Decide" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("[\"", (string)(await Body(response))["message"]!);
    }

    [Fact]
    public async Task Answer_times_come_back_as_utc()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();
        var id = await CreateScenario(client);
        await Put(client, id, "recording", new { value = "Nothing recorded" });

        var progress = await client.GetStringAsync($"/api/scenarios/{id}/progress");

        Assert.Matches("\"time\":\"[0-9T:.-]+Z\"", progress);
    }

    [Fact]
    public async Task Overwrites_keep_history()
    {
        using var factory = new ApiFactory();
        var client = factory.Client();
        var id = await CreateScenario(client);

        await Put(client, id, "recording", new { value = "Nothing recorded" });
        await Put(client, id, "recording", new { value = "Team answers only" });

        using var scope = factory.Services.CreateScope();
        var edits = scope.ServiceProvider.GetRequiredService<BuilderDb>().Edits
            .Where(e => e.ScenarioId == id && e.Target == "recording").OrderBy(e => e.Id).ToList();
        Assert.Equal(2, edits.Count);
        Assert.Null(edits[0].Before);
        Assert.Equal(edits[0].After, edits[1].Before);
        Assert.Equal("\"Team answers only\"", edits[1].After);
    }
}
