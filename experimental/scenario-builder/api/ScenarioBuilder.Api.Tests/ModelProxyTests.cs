// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ScenarioBuilder.Api.Tests;

public class ModelProxyTests
{
    /// <summary>Stands in for the model server: records what it was sent and answers as told.</summary>
    private sealed class Upstream(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return answer(request);
        }
    }

    private static WebApplicationFactory<Program> With(ApiFactory factory, Upstream upstream, params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
                b.UseSetting(key, value);
            b.ConfigureTestServices(s => s.AddHttpClient("models").ConfigurePrimaryHttpMessageHandler(() => upstream));
        });

    private static Upstream Answering(string body = """{"choices":[]}""", string type = "application/json", HttpStatusCode status = HttpStatusCode.OK) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) });

    private static Task<HttpResponseMessage> Chat(HttpClient client, string model) =>
        client.PostAsJsonAsync("/api/model/chat/completions", new { model, stream = true, messages = new[] { new { role = "user", content = "hi" } } });

    [Fact]
    public async Task Lists_configured_models()
    {
        using var factory = new ApiFactory();

        var list = await factory.Client().GetFromJsonAsync<JsonObject>("/api/model/models");

        Assert.Equal("list", (string)list!["object"]!);
        var data = list["data"]!.AsArray();
        Assert.Equal(["gemma4:26b", "sonnet-5.5", "fake"], data.Select(m => (string)m!["id"]!).ToArray());
        Assert.Equal(32768, (int)data[0]!["context_window"]!);
    }

    [Fact]
    public async Task Rewrites_the_model_and_posts_upstream()
    {
        using var factory = new ApiFactory();
        var upstream = Answering();
        using var app = With(factory, upstream, ("ModelServers:Models:0:Model", "gemma4:26b-32k"));

        var response = await Chat(app.CreateClient(), "gemma4:26b");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("http://192.168.65.254:11434/v1/chat/completions", upstream.Request!.RequestUri!.ToString());
        var sent = JsonNode.Parse(upstream.Body!)!;
        Assert.Equal("gemma4:26b-32k", (string)sent["model"]!);
        Assert.True((bool)sent["stream"]!);
        Assert.Null(upstream.Request.Headers.Authorization);
    }

    [Fact]
    public async Task Adds_the_key_from_the_named_variable()
    {
        using var factory = new ApiFactory();
        var upstream = Answering();
        var variable = $"SB_TEST_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(variable, "k");
        using var app = With(factory, upstream, ("ModelServers:Models:1:ApiKeyEnv", variable));

        await Chat(app.CreateClient(), "sonnet-5.5");

        Assert.Equal("http://localhost:4000/v1/chat/completions", upstream.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer k", upstream.Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Streams_the_upstream_body()
    {
        using var factory = new ApiFactory();
        const string events = "data: {\"a\":1}\n\ndata: [DONE]\n\n";
        using var app = With(factory, Answering(events, "text/event-stream"));

        var response = await Chat(app.CreateClient(), "fake");

        Assert.StartsWith("text/event-stream", response.Content.Headers.ContentType!.ToString());
        Assert.Equal(events, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Upstream_error_status_passes_through()
    {
        using var factory = new ApiFactory();
        const string error = """{"error":{"message":"context too long"}}""";
        using var app = With(factory, Answering(error, status: HttpStatusCode.BadRequest));

        var response = await Chat(app.CreateClient(), "fake");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(error, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_model_is_404_in_openai_shape()
    {
        using var factory = new ApiFactory();

        var response = await Chat(factory.Client(), "gpt-9");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var message = (string)(await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!["message"]!;
        Assert.Contains("Configured: gemma4:26b, sonnet-5.5, fake", message);
    }

    [Fact]
    public async Task Unreachable_upstream_is_502()
    {
        using var factory = new ApiFactory();
        using var app = With(factory, new Upstream(_ => throw new HttpRequestException("Connection refused")));

        var response = await Chat(app.CreateClient(), "gemma4:26b");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var message = (string)(await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!["message"]!;
        Assert.Contains("192.168.65.254:11434", message);
        Assert.Contains("Connection refused", message);
    }

    [Fact]
    public void Timeout_is_thirty_minutes()
    {
        using var factory = new ApiFactory();

        var client = factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient("models");

        Assert.Equal(TimeSpan.FromMinutes(30), client.Timeout);
    }
}
