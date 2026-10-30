// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Tests;

/// <summary>The API on a fresh SQLite file and the repository's own kinds.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>experimental/scenario-builder/kinds, from the test assembly's bin folder.</summary>
    public static string KindsDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../kinds"));

    /// <summary>experimental/scenario-builder/libraries, the starter libraries.</summary>
    public static string LibrariesDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../libraries"));

    /// <summary>A fresh file, or <paramref name="database"/> to start a second host on another's file.</summary>
    public ApiFactory(string? database = null) =>
        Database = database ?? Path.Combine(Path.GetTempPath(), $"sb-test-{Guid.NewGuid():N}.db");

    public string Database { get; }

    /// <summary>The web the API fetches URL sources from; set it before the first client.</summary>
    public Func<HttpRequestMessage, HttpResponseMessage>? Web { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Builder", $"Data Source={Database}");
        builder.UseSetting("Kinds:Directory", KindsDirectory);
        builder.UseSetting("Libraries:Directory", LibrariesDirectory);
        if (Web is { } web)
            builder.ConfigureServices(services =>
                services.AddHttpClient(SourceText.WebClient).ConfigurePrimaryHttpMessageHandler(() => new FakeWeb(web)));
    }

    private sealed class FakeWeb(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(answer(request));
    }

    /// <summary>A client calling as <paramref name="user"/>, through the agent when <paramref name="agent"/>.</summary>
    public HttpClient Client(string user = "dev", bool agent = false)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-User", user);
        if (agent)
            client.DefaultRequestHeaders.Add("X-Via", "agent");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { File.Delete(Database); }
        catch (IOException) { }
    }
}
