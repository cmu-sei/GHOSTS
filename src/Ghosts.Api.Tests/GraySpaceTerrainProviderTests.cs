// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Text;
using System.Text.Json;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Tests;

/// <summary>
/// GHOSTS's side of the GraySpace Terrain API: each call goes to its documented path with the API key, sends
/// camelCase JSON, and reads the documented response. An error response becomes a TerrainProviderException.
/// </summary>
public class GraySpaceTerrainProviderTests
{
    private readonly FakeHandler _handler = new();

    [Fact]
    public async Task Creating_an_instance_posts_the_request_with_the_api_key_and_reads_the_instance()
    {
        _handler.Respond(HttpStatusCode.Accepted, """
            {"id":"inst-1","baseId":"office-base","enclave":"office-enclave","status":"queued","progress":0,"jobId":"job-1",
             "log":["queued"],"createdAt":"2026-10-28T09:00:00Z","updatedAt":"2026-10-28T09:00:00Z"}
            """);

        var instance = await Provider().CreateInstanceAsync(new CreateTerrainInstance("office-base", "office-enclave", "exec-7",
            "office-r1", new TerrainExpectation(Hosts: [new TerrainHost("desk-01", "Office LAN")])), default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://grayspace.test/api/v1/instances", request.Uri);
        Assert.Equal("secret", request.ApiKey);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("office-base", body.RootElement.GetProperty("baseId").GetString());
        Assert.Equal("office-enclave", body.RootElement.GetProperty("enclave").GetString());
        Assert.Equal("exec-7", body.RootElement.GetProperty("idempotencyKey").GetString());
        Assert.Equal("desk-01", body.RootElement.GetProperty("expected").GetProperty("hosts")[0].GetProperty("name").GetString());
        Assert.Equal("inst-1", instance.Id);
        Assert.Equal(TerrainStatus.Queued, instance.Status);
        Assert.Equal("job-1", instance.JobId);
    }

    [Fact]
    public async Task The_manifest_is_read_with_every_hosts_placement_and_role()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"instanceId":"inst-1","baseId":"office-base","enclave":"office-enclave","directoryDomain":"office.local",
             "hosts":[{"name":"desk-01","segment":"Office LAN","rangeIp":"10.20.0.11","managementIp":"172.30.0.11",
                       "osFamily":"linux","osVersion":"ubuntu-24.04","role":"workstation","proxmoxNode":"pve-01","vmid":1011,
                       "ghostsClient":true,"participantWorkstation":false}],
             "sites":[{"name":"Chirper","kind":"pandora","url":"http://chirper.office.local"}]}
            """);

        var manifest = await Provider().GetManifestAsync("inst-1", default);

        Assert.Equal("http://grayspace.test/api/v1/instances/inst-1/manifest", Assert.Single(_handler.Requests).Uri);
        var host = Assert.Single(manifest.Hosts);
        Assert.Equal("desk-01", host.Name);
        Assert.Equal(1011, host.Vmid);
        Assert.True(host.GhostsClient);
        Assert.Equal("pandora", Assert.Single(manifest.Sites).Kind);
    }

    [Fact]
    public async Task Tearing_down_sends_a_delete_for_the_instance()
    {
        _handler.Respond(HttpStatusCode.Accepted, """
            {"id":"inst-1","baseId":"office-base","enclave":"office-enclave","status":"tearing-down","progress":0,"jobId":"job-2",
             "log":[],"createdAt":"2026-10-28T09:00:00Z","updatedAt":"2026-10-28T10:00:00Z"}
            """);

        var instance = await Provider().DeleteInstanceAsync("inst-1", default);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("http://grayspace.test/api/v1/instances/inst-1", request.Uri);
        Assert.Equal(TerrainStatus.TearingDown, instance.Status);
    }

    [Fact]
    public async Task Bases_and_enclaves_are_listed_from_their_paths()
    {
        _handler.Respond(HttpStatusCode.OK, """[{"id":"office-base","name":"Office base","version":"2","proxmoxNode":"pve-01","status":"running"}]""");
        _handler.Respond(HttpStatusCode.OK, """[{"name":"office-enclave","version":"1","baseId":"office-base","hostCount":12}]""");

        var bases = await Provider().ListBasesAsync(default);
        var enclaves = await Provider().ListEnclavesAsync(default);

        Assert.Equal(["http://grayspace.test/api/v1/bases", "http://grayspace.test/api/v1/enclaves"], _handler.Requests.Select(r => r.Uri));
        Assert.Equal("office-base", Assert.Single(bases).Id);
        Assert.Equal(12, Assert.Single(enclaves).HostCount);
    }

    [Fact]
    public async Task An_error_response_raises_with_the_providers_status_and_code()
    {
        _handler.Respond(HttpStatusCode.NotFound, """{"code":"instance_not_found","message":"No instance inst-9"}""");

        var error = await Assert.ThrowsAsync<TerrainProviderException>(() => Provider().GetInstanceAsync("inst-9", default));

        Assert.Equal(404, error.Status);
        Assert.Equal("instance_not_found", error.Code);
    }

    private GraySpaceTerrainProvider Provider() => new(new HttpClient(_handler),
        Options.Create(new TerrainProviderOptions { BaseUrl = "http://grayspace.test/", ApiKey = "secret" }));

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();
        public List<(HttpMethod Method, string Uri, string? ApiKey, string? Body)> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) => _responses.Enqueue((status, body));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!.ToString(),
                request.Headers.TryGetValues("X-Api-Key", out var k) ? k.Single() : null,
                request.Content == null ? null : await request.Content.ReadAsStringAsync(ct)));
            var (status, body) = _responses.Dequeue();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    [Fact]
    public void The_shipped_settings_leave_the_stub_off_so_an_unconfigured_range_fails_loudly()
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
}
