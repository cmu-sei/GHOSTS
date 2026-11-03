// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Endpoints.Model;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>Why the model couldn't draft: its server failed, or its replies kept breaking a rule.</summary>
public sealed class DraftFailed(string message) : Exception(message);

/// <summary>
/// Has a model write a kind's draft for an admin: first the stages, then each stage's questions. The kind rules are
/// enforced here, not left to the prompt: a reply that breaks one goes back to the model with what's wrong, up to
/// <see cref="Tries"/> replies in all. What it writes only ever reaches the draft; publishing is the admin's.
/// </summary>
public sealed class KindDrafter(IHttpClientFactory clients, IOptions<ModelOptions> options)
{
    public const int Tries = 3;
    public const int MaxStages = 8;
    public const int MaxQuestions = 15;

    /// <summary>Drafted answers go under /scenario, which the scenario document's schema leaves open.</summary>
    private const string Section = "/scenario";

    private const string SummaryField = "executiveSummary";

    public bool Knows(string? model) => options.Value.Models.Any(m => m.Name == model);

    /// <summary>
    /// <paramref name="draft"/> with new stages and no questions yet, and its executive summary due after one of them.
    /// <paramref name="example"/> shows the model how a kind is staged.
    /// </summary>
    public Task<Kind> PlanStages(string model, Kind draft, Kind example, CancellationToken ct)
    {
        const string system = """
            You design kinds of exercise scenario for a guided scenario builder. A kind is the interview a designer goes
            through to build one scenario of that kind: a few stages, each with its own questions. A stage ends at a gate,
            and a gate may need the scenario owner's approval, named as a short instruction such as "Approve the
            executive summary". After one of the stages there is enough to write an executive summary of the scenario.

            Plan the stages of the kind you're given: 3 to 6 stages, in the order a designer settles things. Give each a
            short title in Title Case and one sentence on what it settles.

            Reply with one JSON object and nothing else:
            {"stages": [{"title": "...", "purpose": "...", "approval": "..." or null}], "summaryAfterStage": 0}
            summaryAfterStage is the number of the stage, counting from 0, after which the executive summary is written.
            """;
        var staging = example.Stages.Select(s => new { title = s.Title, approval = s.Gate.Approval });
        var user = $"""
            {About(draft)}

            For format and depth, this is how the "{example.Name}" kind is staged:
            {JsonSerializer.Serialize(staging, KindCatalog.Json)}
            Executive summary after stage {example.Drafts.FirstOrDefault(d => d.Field == SummaryField)?.DueAfterStage ?? 0}.
            """;
        return Ask<Kind>(model, system, user, reply =>
        {
            if (reply["stages"] is not JsonArray stages || stages.Count is 0 or > MaxStages)
                return (null, $"\"stages\" must be a list of 1 to {MaxStages} stages.");
            var planned = new List<KindStage>();
            foreach (var node in stages)
            {
                var title = Text(node?["title"]);
                if (title is null)
                    return (null, $"stage {planned.Count} has no title.");
                if (planned.Any(p => p.Title == title))
                    return (null, $"two stages are titled \"{title}\".");
                planned.Add(new KindStage(planned.Count, title, [], new KindGate(Text(node?["approval"])), Text(node?["purpose"]) ?? ""));
            }
            var summary = reply["summaryAfterStage"] is JsonValue v && v.TryGetValue<int>(out var n) ? Math.Clamp(n, 0, planned.Count - 1) : Math.Min(2, planned.Count - 1);
            return (draft with { Stages = planned, Drafts = [new KindDraft(SummaryField, summary, $"{Section}/{SummaryField}", "excon")] }, null);
        }, ct);
    }

    /// <summary><paramref name="draft"/> with stage <paramref name="number"/>'s questions written by the model.</summary>
    public Task<Kind> WriteStage(string model, Kind draft, int number, Kind example, CancellationToken ct)
    {
        const string system = """
            You write the questions for one stage of a kind of exercise scenario, for a guided scenario builder. A designer
            answers them one at a time, with an agent's help, and the answers become the scenario.

            Each question is a JSON object with:
            - "id": camelCase letters and digits, starting with a lower-case letter, unique in the whole kind
            - "label": a short name in Title Case
            - "text": the question as it is asked, in one or two sentences
            - "why": one sentence on why the answer matters later
            - "answer": "text" for free text, "list" for several items (one per line), or "choice" for one of "options"
            - "options": for "choice" only, 2 to 6 short options
            - "recommended": optional, a suggested answer; for "choice" it must be one of the options
            - "visibility": optional; "excon" when participants must not see the answer because it gives the game away,
              "designer" for designers only; leave it out otherwise

            Write 3 to 10 questions for the stage you're given. Don't ask what earlier stages already ask.

            Reply with one JSON object and nothing else: {"questions": [ ... ]}
            """;
        var stage = draft.Stages[number];
        var asked = draft.Stages.Where(s => s.Number != number).SelectMany(s => s.Questions).Select(q => $"- {q.Id}: {q.Text}").ToList();
        var shown = example.Stages[Math.Min(number, example.Stages.Count - 1)];
        var sample = shown.Questions.Select(q => new { q.Id, q.Label, q.Text, q.Why, q.Answer, q.Options, q.Recommended, q.Visibility });
        var user = $"""
            {About(draft)}

            Its stages:
            {string.Join("\n", draft.Stages.Select(s => $"{s.Number}. {s.Title}: {s.Purpose}"))}

            Write stage {number}, "{stage.Title}". It settles: {stage.Purpose}

            Other stages already ask:
            {(asked.Count > 0 ? string.Join("\n", asked) : "- nothing yet")}

            For format and depth, this is the "{shown.Title}" stage of the "{example.Name}" kind:
            {JsonSerializer.Serialize(sample, new JsonSerializerOptions(KindCatalog.Json) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull })}
            """;
        return Ask<Kind>(model, system, user, reply =>
        {
            if (reply["questions"] is not JsonArray questions || questions.Count is 0 or > MaxQuestions)
                return (null, $"\"questions\" must be a list of 1 to {MaxQuestions} questions.");
            var written = new List<KindQuestion>();
            foreach (var node in questions)
            {
                KindQuestion? q;
                try
                {
                    q = node?.Deserialize<KindQuestion>(KindCatalog.Json);
                }
                catch (JsonException e)
                {
                    return (null, $"question {written.Count + 1} can't be read: {e.Message}");
                }
                if (q is null)
                    return (null, $"question {written.Count + 1} is empty.");
                written.Add(q with { Path = $"{Section}/{q.Id}" });
            }
            var stages = draft.Stages.Select(s => s.Number == number ? s with { Questions = written } : s).ToList();
            var candidate = (draft with { Stages = stages }).Filled(draft.Id, draft.Version);
            return KindCatalog.Problem(candidate, finished: false) is { } problem ? (null, problem) : (candidate, null);
        }, ct);
    }

    private static string About(Kind draft) => $"""
        Kind: {draft.Name}
        What it is: {draft.Description}
        Its scenarios {(draft.Topology ? "build a range (a network) to play on" : "need no range")}.
        """;

    private static string? Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    /// <summary>Asks until a reply passes <paramref name="check"/>, which returns the result or what's wrong with the reply.</summary>
    private async Task<T> Ask<T>(string model, string system, string user, Func<JsonObject, (T? Result, string? Problem)> check, CancellationToken ct)
        where T : class
    {
        var entry = options.Value.Models.First(m => m.Name == model);
        var messages = new JsonArray(Message("system", system), Message("user", user));
        string? problem = null;
        for (var attempt = 1; attempt <= Tries; attempt++)
        {
            var (reply, finish) = await Complete(entry, messages, ct);
            if (finish == "length")
                problem = "the reply was cut off before it ended, at the model's length limit. Keep it shorter: fewer questions, a sentence each.";
            else if (string.IsNullOrWhiteSpace(reply))
                problem = "the reply was empty.";
            else if (Unreadable(reply, out var json) is { } unreadable)
                problem = unreadable;
            else
            {
                var (result, wrong) = check(json!);
                if (wrong is null)
                    return result!;
                problem = wrong;
            }
            messages.Add(Message("assistant", reply));
            messages.Add(Message("user", $"That reply can't be used: {problem} Reply again with the whole JSON object, corrected, and nothing else."));
        }
        throw new DraftFailed($"After {Tries} replies, the model's draft still breaks a rule: {problem}");
    }

    private static JsonObject Message(string role, string content) => new() { ["role"] = role, ["content"] = content };

    /// <summary>The reply's text, and why the model server stopped it ("stop", or "length" when it hit its limit).</summary>
    private async Task<(string Content, string? Finish)> Complete(ModelEntry entry, JsonArray messages, CancellationToken ct)
    {
        var body = new JsonObject { ["model"] = entry.Model, ["messages"] = messages.DeepClone(), ["stream"] = false };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{entry.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(entry.ApiKeyEnv) && Environment.GetEnvironmentVariable(entry.ApiKeyEnv) is { Length: > 0 } key)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        HttpResponseMessage response;
        try
        {
            response = await clients.CreateClient(ChatCompletionsEndpoint.Client).SendAsync(request, ct);
        }
        catch (HttpRequestException e)
        {
            throw new DraftFailed($"Can't reach the model server at {entry.BaseUrl}: {e.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DraftFailed($"The model server at {entry.BaseUrl} didn't answer within {options.Value.TimeoutMinutes} minutes.");
        }
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new DraftFailed($"The model server at {entry.BaseUrl} answered {(int)response.StatusCode}: {text}");
        try
        {
            var choice = JsonNode.Parse(text)?["choices"]?[0];
            return ((string?)choice?["message"]?["content"] ?? "", (string?)choice?["finish_reason"]);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            throw new DraftFailed($"The model server at {entry.BaseUrl} answered with something that isn't a chat completion.");
        }
    }

    /// <summary>
    /// Null when the reply holds a JSON object, fenced or not, from its first "{" to its last "}"; otherwise what's
    /// wrong with it, in words the model can act on.
    /// </summary>
    private static string? Unreadable(string reply, out JsonObject? json)
    {
        json = null;
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start)
            return "the reply has no JSON object in it.";
        try
        {
            json = JsonNode.Parse(reply[start..(end + 1)]) as JsonObject;
            return json is null ? "the reply's JSON isn't an object." : null;
        }
        catch (JsonException e)
        {
            return $"the reply's JSON can't be read: {e.Message}";
        }
    }
}
