// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class KindDraftTests
{
    /// <summary>Stands in for the model server: gives its replies in order, and keeps what it was sent.</summary>
    private sealed class Model(params string[] replies) : HttpMessageHandler
    {
        private readonly Queue<string> _replies = new(replies);
        public List<JsonObject> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Sent.Add(JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!.AsObject());
            var content = _replies.Count > 1 ? _replies.Dequeue() : _replies.Peek();
            // A reply starting "CUT:" stands for one the model server stopped at its length limit.
            var finish = content.StartsWith("CUT:") ? "length" : "stop";
            var body = new JsonObject
            {
                ["choices"] = new JsonArray(new JsonObject
                {
                    ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = content.Replace("CUT:", "") }, ["finish_reason"] = finish
                })
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        }

        /// <summary>Everything the model was told in one request, as one string.</summary>
        public string Told(int request) => string.Join("\n", Sent[request]["messages"]!.AsArray().Select(m => (string)m!["content"]!));
    }

    private static HttpClient Admin(ApiFactory factory, Model model) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddHttpClient("models").ConfigurePrimaryHttpMessageHandler(() => model))).CreateClient();

    private const string Stages = """
        Here you go:
        ```json
        { "stages": [
            { "title": "Target and Rules", "purpose": "What the team attacks and the rules of engagement.", "approval": "Confirm the rules of engagement" },
            { "title": "Story", "purpose": "The premise and the ground truth.", "approval": null } ],
          "summaryAfterStage": 1 }
        ```
        """;

    private const string TargetQuestions = """
        { "questions": [
            { "id": "target", "label": "Target", "text": "What does the team attack?", "why": "Everything aims at it.", "answer": "text" },
            { "id": "rules", "text": "Which rules of engagement apply?", "why": "They keep the range safe.", "answer": "choice", "options": ["Strict", "Loose"], "recommended": "Strict" } ] }
        """;

    private static async Task<string> Create(HttpClient admin, string? basedOn = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/kinds", new { name = "Red Team Exercise", description = "A small team attacks a mock enterprise.", topology = true, basedOn });
        response.EnsureSuccessStatusCode();
        return (string)(await Body(response))["id"]!;
    }

    [Fact]
    public async Task The_model_plans_the_stages_then_writes_each_stage()
    {
        using var factory = new ApiFactory();
        var model = new Model(Stages, TargetQuestions);
        var admin = Admin(factory, model);
        var id = await Create(admin, basedOn: Kind);

        var planned = await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages", new { model = "fake" });
        Assert.Equal(HttpStatusCode.OK, planned.StatusCode);
        var kind = (await Body(planned))["kind"]!;
        Assert.Equal(["Target and Rules", "Story"], kind["stages"]!.AsArray().Select(s => (string)s!["title"]!));
        Assert.Equal("Confirm the rules of engagement", (string)kind["stages"]![0]!["gate"]!["approval"]!);
        Assert.Equal("The premise and the ground truth.", (string)kind["stages"]![1]!["purpose"]!);
        Assert.Empty(kind["stages"]![0]!["questions"]!.AsArray());
        Assert.Equal(1, (int)kind["drafts"]![0]!["dueAfterStage"]!);
        Assert.Equal("fake", (string)model.Sent[0]["model"]!);
        Assert.Contains("A small team attacks a mock enterprise.", model.Told(0));
        Assert.Contains("Engagement Context", model.Told(0));

        var written = await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages/0", new { model = "fake" });
        Assert.Equal(HttpStatusCode.OK, written.StatusCode);
        var questions = (await Body(written))["kind"]!["stages"]![0]!["questions"]!.AsArray();
        Assert.Equal(["target", "rules"], questions.Select(q => (string)q!["id"]!));
        Assert.Equal("/scenario/target", (string)questions[0]!["path"]!);
        Assert.Equal("Rules", (string)questions[1]!["label"]!);
        Assert.Contains("Target and Rules", model.Told(1));
        Assert.Contains("What the team attacks and the rules of engagement.", model.Told(1));
    }

    [Fact]
    public async Task A_reply_that_breaks_a_rule_goes_back_to_the_model_to_fix()
    {
        using var factory = new ApiFactory();
        var broken = """{ "questions": [ { "id": "rules", "text": "Which rules?", "why": "w", "answer": "choice" } ] }""";
        var model = new Model(Stages, broken, TargetQuestions);
        var admin = Admin(factory, model);
        var id = await Create(admin);
        (await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages", new { model = "fake" })).EnsureSuccessStatusCode();

        var written = await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages/0", new { model = "fake" });

        Assert.Equal(HttpStatusCode.OK, written.StatusCode);
        Assert.Equal(3, model.Sent.Count);
        Assert.Contains("has no options", model.Told(2));
    }

    [Fact]
    public async Task The_model_is_told_when_its_reply_was_cut_off_or_its_json_is_broken()
    {
        using var factory = new ApiFactory();
        var model = new Model(Stages, """CUT:{ "questions": [ { "id": "target", "text": "What""", """{ "questions": [ { "id": "target" "text": "What?" } ] }""", TargetQuestions);
        var admin = Admin(factory, model);
        var id = await Create(admin);
        (await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages", new { model = "fake" })).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages/0", new { model = "fake" })).EnsureSuccessStatusCode();

        string LastSaid(int request) => (string)model.Sent[request]["messages"]!.AsArray()[^1]!["content"]!;
        Assert.Contains("cut off", LastSaid(2));
        Assert.Contains("JSON can't be read", LastSaid(3));
    }

    [Fact]
    public async Task It_gives_up_after_three_tries_and_keeps_the_draft()
    {
        using var factory = new ApiFactory();
        var model = new Model(Stages, "I can't do JSON today.");
        var admin = Admin(factory, model);
        var id = await Create(admin);
        (await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages", new { model = "fake" })).EnsureSuccessStatusCode();

        var written = await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages/0", new { model = "fake" });

        Assert.Equal(HttpStatusCode.BadGateway, written.StatusCode);
        var error = await Body(written);
        Assert.Equal("MODEL_FAILED", (string)error["code"]!);
        Assert.Contains("has no JSON object", (string)error["message"]!);
        Assert.Equal(4, model.Sent.Count);
        var kind = (await admin.GetFromJsonAsync<JsonObject>($"/api/admin/kinds/{id}"))!["kind"]!;
        Assert.Equal(2, kind["stages"]!.AsArray().Count);
        Assert.Empty(kind["stages"]![0]!["questions"]!.AsArray());
    }

    [Fact]
    public async Task An_unknown_model_or_stage_is_refused()
    {
        using var factory = new ApiFactory();
        var admin = Admin(factory, new Model(Stages));
        var id = await Create(admin);

        Assert.Equal("MODEL_UNKNOWN", await Code(await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages", new { model = "nope" })));
        Assert.Equal("STAGE_UNKNOWN", await Code(await admin.PostAsJsonAsync($"/api/admin/kinds/{id}/draft/stages/0", new { model = "fake" })));
    }
}
