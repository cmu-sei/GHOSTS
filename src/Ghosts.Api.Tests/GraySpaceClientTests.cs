// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Text;
using System.Text.Json;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Tests;

/// <summary>
/// GHOSTS's client for GraySpace Terrain API 1.3.0: bearer token, the real routes, snake_case on the wire (label keys
/// as they are), and GraySpace's errors as provider errors GHOSTS can report.
/// </summary>
public class GraySpaceClientTests
{
    private readonly FakeHandler _handler = new();

    private const string Instance = """
        {"id":"g7","slice":{"slug":"office-day","version":"sha256:ab"},"base":{"name":"grayspace/2026.10","csv_sha256":"c1","scope":"tier1"},
         "state":"ready","created":"2026-10-29T09:00:00Z","updated":"2026-10-29T09:05:00Z","stages_done":["validate","ready"],"jobs":["j-1"],
         "sites":[{"name":"office","router":"lum-chi","isp":"Lumen","location":"Chicago","wan_ip":"209.151.130.2","edge":"g7-office-edge",
                   "segments":[{"name":"office-lan","cidr":"192.168.10.0/24","gateway":"192.168.10.1"}]}],
         "hosts":[{"name":"staff-01","hostname":"g7-staff-01","site":"office","segment":"office-lan","ip":"192.168.10.10","public_ip":null,
                   "mgmt_ip":"10.0.5.20","fqdns":[],"roles":["ghosts-client"],"labels":{"ghosts.pool":"office staff"},"image":"ubuntu-22.04-lxc","status":"running"}],
         "dns":[],"certificates":[]}
        """;

    [Fact]
    public async Task Calls_carry_the_bearer_token_and_use_the_1_3_0_routes()
    {
        _handler.Respond(HttpStatusCode.OK, """{"version":null,"status":"not_deployed","routers":{"expected":0,"running":null},"bgp":{"total":null,"established":null}}""");
        _handler.Respond(HttpStatusCode.Accepted, """{"instance":"g7","job":{"id":"j-1","kind":"create_instance","target":"g7","state":"queued","stages":[],"created":"2026-10-29T09:00:00Z"}}""");
        _handler.Respond(HttpStatusCode.OK, "line one\nline two\n");
        _handler.Respond(HttpStatusCode.OK, """{"type":"FeatureCollection","features":[]}""");
        var client = Client();

        await client.GetBaseAsync(default);
        var accepted = await client.CreateInstanceAsync(new CreateInstanceRequest("office-day", "sha256:ab", "g7"), default);
        var log = await client.GetJobLogAsync("j-1", default);
        var map = await client.GetMapAsync("g7", default);

        Assert.All(_handler.Requests, r => Assert.Equal("Bearer dev-token", r.Authorization));
        Assert.Equal(["GET http://gs/api/v1/base", "POST http://gs/api/v1/instances", "GET http://gs/api/v1/jobs/j-1/log?follow=false", "GET http://gs/api/v1/map?instance=g7"],
            _handler.Requests.Select(r => $"{r.Method} {r.Uri}"));
        Assert.Equal("""{"slice":"office-day","version":"sha256:ab","prefix":"g7"}""", _handler.Requests[1].Body);
        Assert.Equal(("g7", "j-1", JobState.Queued), (accepted.Instance, accepted.Job.Id, accepted.Job.State));
        Assert.Equal("line one\nline two\n", log);
        Assert.Equal("FeatureCollection", JsonDocument.Parse(map).RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task An_instance_is_read_from_graySpaces_snake_case_with_its_labels_as_they_are()
    {
        _handler.Respond(HttpStatusCode.OK, Instance);

        var instance = await Client().GetInstanceAsync("g7", default);

        var host = Assert.Single(instance.Hosts);
        Assert.Equal(("g7-staff-01", "10.0.5.20", "office staff"), (host.Hostname, host.MgmtIp, host.Labels["ghosts.pool"]));
        Assert.Equal("209.151.130.2", Assert.Single(instance.Sites).WanIp);
        Assert.Equal(["validate", "ready"], instance.StagesDone);
        Assert.Equal(InstanceState.Ready, instance.State);
    }

    [Fact]
    public async Task A_slice_spec_is_sent_as_graySpace_reads_it()
    {
        _handler.Respond(HttpStatusCode.OK, """{"slug":"office-day","version":"sha256:ab","created":true,"findings":[]}""");
        var spec = new SliceSpec("office-day", "od", "An ordinary day at the office.", "ghosts",
            [new SliceSite("office", new SliceAttach(), [new SliceSegment("office-lan")])],
            [new SliceHost("staff", "ubuntu-22.04-lxc", Count: 2, Segment: "office-lan", Roles: ["ghosts-client"], Labels: new() { ["ghosts.pool"] = "office staff" })],
            new() { ["ghosts-client"] = new() { ["api_url"] = "http://ghosts:5000/api", ["client_url"] = "http://ghosts:5000/client.zip" } });

        var put = await Client().PutSliceAsync(spec, default);

        var request = _handler.Requests.Single();
        Assert.Equal("PUT http://gs/api/v1/slices/office-day", $"{request.Method} {request.Uri}");
        Assert.Empty(GraySpaceContract.SliceErrors(request.Body!));
        Assert.Contains("\"apiVersion\":\"grayspace/v1\"", request.Body);
        Assert.Contains("\"role_vars\":{\"ghosts-client\":", request.Body);
        Assert.Contains("\"ghosts.pool\":\"office staff\"", request.Body);
        Assert.Equal("sha256:ab", put.Version);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, """{"detail":"Instance 'g7' is provisioning"}""", 409, "conflict", "Instance 'g7' is provisioning")]
    [InlineData(HttpStatusCode.NotFound, """{"detail":"No job 'j-9'"}""", 404, "not_found", "No job 'j-9'")]
    [InlineData(HttpStatusCode.ServiceUnavailable, """{"detail":"GRAYSPACE_API_TOKEN is not configured"}""", 503, "unavailable", "GRAYSPACE_API_TOKEN is not configured")]
    [InlineData((HttpStatusCode)422, """{"detail":[{"loc":["body","slice"],"msg":"Field required","type":"missing"}]}""", 422, "invalid_request", "body.slice: Field required")]
    public async Task GraySpace_errors_become_provider_errors(HttpStatusCode status, string body, int code, string kind, string message)
    {
        _handler.Respond(status, body);

        var ex = await Assert.ThrowsAsync<TerrainProviderException>(() => Client().GetJobAsync("j-9", default));

        Assert.Equal((code, kind, message), (ex.Status, ex.Code, ex.Detail));
    }

    [Fact]
    public async Task A_create_graySpace_refuses_carries_its_findings()
    {
        _handler.Respond((HttpStatusCode)422, """{"detail":{"message":"The slice can't be instantiated","findings":[{"code":"CAPACITY","severity":"error","path":"hosts","message":"The node lacks 40 GB of storage"}]}}""");

        var ex = await Assert.ThrowsAsync<TerrainProviderException>(() => Client().CreateInstanceAsync(new CreateInstanceRequest("office-day"), default));

        Assert.Equal((422, "The slice can't be instantiated"), (ex.Status, ex.Detail));
        Assert.Equal("CAPACITY", Assert.Single(ex.Findings!).Code);
    }

    [Fact]
    public async Task An_unreachable_graySpace_is_a_provider_error_not_a_crash()
    {
        _handler.Fail(new HttpRequestException("Connection refused"));

        var ex = await Assert.ThrowsAsync<TerrainProviderException>(() => Client().GetBaseAsync(default));

        Assert.Equal((0, "unreachable"), (ex.Status, ex.Code));
    }

    [Fact]
    public async Task Without_a_base_url_nothing_is_sent()
    {
        var ex = await Assert.ThrowsAsync<TerrainProviderException>(() => Client(baseUrl: "").GetBaseAsync(default));

        Assert.Equal("not_configured", ex.Code);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public void The_shipped_settings_leave_the_simulator_off_so_an_unconfigured_range_fails_loudly()
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(ShippedSettings()));
        var terrain = settings.RootElement.GetProperty("TerrainProvider");

        Assert.False(terrain.GetProperty("EnableStub").GetBoolean());
        Assert.Equal(string.Empty, terrain.GetProperty("BaseUrl").GetString());
    }

    private static string ShippedSettings()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Ghosts.Api", "appsettings.json");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("src/Ghosts.Api/appsettings.json not found above the test binaries");
    }

    private GraySpaceClient Client(string baseUrl = "http://gs") =>
        new(new HttpClient(_handler), Options.Create(new TerrainProviderOptions { BaseUrl = baseUrl, Token = "dev-token" }));

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
        private Exception? _failure;
        public List<(HttpMethod Method, string Uri, string? Authorization, string? Body)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

        public void Fail(Exception failure) => _failure = failure;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(),
                request.Content == null ? null : await request.Content.ReadAsStringAsync(ct)));
            if (_failure != null) throw _failure;
            var (status, body) = _responses.Dequeue();
            var type = body.StartsWith('{') ? "application/json" : "text/plain";
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, type) };
        }
    }
}
