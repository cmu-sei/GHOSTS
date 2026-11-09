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

public class GraySpaceTests
{
    private const string Address = "http://gs.test:8000";
    private const string Token = "t0k-secret";

    /// <summary>Stands in for GraySpace: answers "METHOD /path?query" as told (404 otherwise), and records every call.</summary>
    private sealed class FakeGraySpace : HttpMessageHandler
    {
        public Dictionary<string, Func<HttpResponseMessage>> Routes { get; } = new()
        {
            ["GET /api/v1/server"] = () => Json("""{"jobs":true,"slice_pool":"192.168.0.0/16","images":[],"roles":[],"base":null}"""),
            ["GET /api/v1/capabilities"] = () => Json("""{"dns":{"tlds":["com"],"hosting":[],"resolvers":[]},"cas":[],"isps":[]}""")
        };

        public List<(string Route, string? Auth, string? Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var route = $"{request.Method.Method} {request.RequestUri!.PathAndQuery}";
            Calls.Add((route, request.Headers.Authorization?.ToString(), request.Content is null ? null : await request.Content.ReadAsStringAsync(ct)));
            return Routes.TryGetValue(route, out var answer) ? answer() : Json("""{"detail":"Not Found"}""", HttpStatusCode.NotFound);
        }

        public JsonObject BodyOf(string route) => JsonNode.Parse(Calls.Last(c => c.Route == route).Body!)!.AsObject();
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static WebApplicationFactory<Program> With(ApiFactory factory, FakeGraySpace grayspace) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddHttpClient("grayspace").ConfigurePrimaryHttpMessageHandler(() => grayspace)));

    private static HttpClient Client(WebApplicationFactory<Program> app, string user = "dev", bool agent = false)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-User", user);
        if (agent)
            client.DefaultRequestHeaders.Add("X-Via", "agent");
        return client;
    }

    private static Task<HttpResponseMessage> Connect(HttpClient client, string url = Address, string token = Token) =>
        client.PutAsJsonAsync("/api/grayspace", new { url, token });

    /// <summary>A connected GraySpace and a scenario, ready for enclave calls.</summary>
    private static async Task<(HttpClient Client, int Scenario)> Ready(WebApplicationFactory<Program> app)
    {
        var client = Client(app);
        (await Connect(client)).EnsureSuccessStatusCode();
        return (client, await CreateScenario(client, scenarioTypeId: SmallUnitType));
    }

    [Fact]
    public async Task An_admin_connects_once_GraySpace_accepts_the_token_which_never_comes_back()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        using var app = With(factory, grayspace);

        var connected = await Connect(Client(app), $"{Address}/api/v1/");

        Assert.Equal(HttpStatusCode.OK, connected.StatusCode);
        Assert.Equal(("GET /api/v1/server", $"Bearer {Token}"), (grayspace.Calls.Single().Route, grayspace.Calls.Single().Auth));
        var status = await Client(app, user: "lee").GetStringAsync("/api/grayspace");
        Assert.DoesNotContain(Token, await connected.Content.ReadAsStringAsync());
        Assert.DoesNotContain(Token, status);
        var view = JsonNode.Parse(status)!;
        Assert.True((bool)view["connected"]!);
        Assert.Equal(Address, (string)view["url"]!);
        Assert.Equal("dev", (string)view["connectedBy"]!);
    }

    [Fact]
    public async Task Only_an_admin_connects_or_disconnects_and_the_agent_never_touches_GraySpace()
    {
        using var factory = new ApiFactory();
        using var app = With(factory, new FakeGraySpace());
        var (_, id) = await Ready(app);

        Assert.Equal("NOT_ADMIN", await Code(await Connect(Client(app, user: "lee"))));
        Assert.Equal("NOT_ADMIN", await Code(await Client(app, user: "lee").DeleteAsync("/api/grayspace")));
        var agent = Client(app, agent: true);
        Assert.Equal("AGENT_CANNOT_DEPLOY", await Code(await agent.GetAsync("/api/grayspace/offers")));
        Assert.Equal("AGENT_CANNOT_DEPLOY", await Code(await agent.GetAsync($"/api/scenarios/{id}/enclave")));
        Assert.Equal("AGENT_CANNOT_DEPLOY", await Code(await agent.PostAsync($"/api/scenarios/{id}/enclave/save", null)));

        Assert.Equal(HttpStatusCode.NoContent, (await Client(app).DeleteAsync("/api/grayspace")).StatusCode);
        Assert.False((bool)(await Client(app).GetFromJsonAsync<JsonObject>("/api/grayspace"))!["connected"]!);
    }

    [Fact]
    public async Task A_refused_token_or_an_unreachable_GraySpace_saves_nothing()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        using var app = With(factory, grayspace);
        var admin = Client(app);

        grayspace.Routes["GET /api/v1/server"] = () => Json("""{"detail":"Missing or invalid bearer token"}""", HttpStatusCode.Unauthorized);
        var refused = await Connect(admin);
        grayspace.Routes["GET /api/v1/server"] = () => throw new HttpRequestException("Connection refused");
        var unreachable = await Connect(admin);

        Assert.Equal((HttpStatusCode.BadGateway, "GRAYSPACE_TOKEN_REJECTED"), (refused.StatusCode, await Code(refused)));
        Assert.Equal((HttpStatusCode.BadGateway, "GRAYSPACE_UNREACHABLE"), (unreachable.StatusCode, await Code(unreachable)));
        Assert.Equal("URL_INVALID", await Code(await Connect(admin, url: "gs.test")));
        Assert.Equal("TOKEN_REQUIRED", await Code(await Connect(admin, token: " ")));
        Assert.False((bool)(await admin.GetFromJsonAsync<JsonObject>("/api/grayspace"))!["connected"]!);
        Assert.Equal("NOT_CONNECTED", await Code(await admin.GetAsync("/api/grayspace/offers")));
    }

    [Fact]
    public async Task Offers_pass_GraySpace_through_and_say_which_parts_it_did_not_give()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["GET /api/v1/base"] = () => Json("""{"status":"healthy","routers":{"expected":3,"running":3}}""");
        grayspace.Routes["GET /api/v1/map"] = () => Json("""{"type":"FeatureCollection","features":[]}""");
        grayspace.Routes["GET /api/v1/slices"] = () => Json("""[{"slug":"other","kind":"slice","description":""}]""");
        using var app = With(factory, grayspace);
        var (client, _) = await Ready(app);

        var offers = (await client.GetFromJsonAsync<JsonObject>("/api/grayspace/offers"))!;

        Assert.Equal("192.168.0.0/16", (string)offers["server"]!["slice_pool"]!);
        Assert.Equal("com", (string)offers["capabilities"]!["dns"]!["tlds"]![0]!);
        Assert.Equal("healthy", (string)offers["base"]!["status"]!);
        Assert.Equal("other", (string)offers["slices"]![0]!["slug"]!);
        Assert.Null(offers["attachmentPoints"]);
        Assert.Contains("Not Found", (string)offers["problems"]!["attachmentPoints"]!);
    }

    [Fact]
    public async Task With_no_base_deployed_it_connects_and_offers_what_the_server_has()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["GET /api/v1/capabilities"] = () => Json(
            """{"detail":"The deployed base version is unknown or not a shipped version; name one with ?version="}""", HttpStatusCode.Conflict);
        using var app = With(factory, grayspace);
        var (client, _) = await Ready(app);

        var offers = (await client.GetFromJsonAsync<JsonObject>("/api/grayspace/offers"))!;

        Assert.Equal("192.168.0.0/16", (string)offers["server"]!["slice_pool"]!);
        Assert.Null(offers["capabilities"]);
        Assert.Contains("name one with ?version=", (string)offers["problems"]!["capabilities"]!);
    }

    [Fact]
    public async Task A_new_enclave_is_named_from_the_scenario_and_the_draft_is_kept()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        using var app = With(factory, grayspace);
        var (client, id) = await Ready(app);

        var fresh = (await client.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}/enclave"))!;
        var spec = fresh["spec"]!.AsObject();
        spec["hosts"] = new JsonArray(new JsonObject { ["name"] = "web", ["image"] = "ubuntu-24.04-vm" });
        var put = await client.PutAsJsonAsync($"/api/scenarios/{id}/enclave", new { spec });
        var kept = (await client.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}/enclave"))!;

        Assert.Equal(("grayspace/v1", "Slice", "harbor-blackout", "harborbl"),
            ((string)fresh["spec"]!["apiVersion"]!, (string)fresh["spec"]!["kind"]!, (string)fresh["spec"]!["slug"]!, (string)fresh["spec"]!["prefix"]!));
        Assert.Null(fresh["updatedBy"]);
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("web", (string)kept["spec"]!["hosts"]![0]!["name"]!);
        Assert.Equal("dev", (string)kept["updatedBy"]!);
        Assert.Single(grayspace.Calls);   // only Connect's server call: editing the draft calls nothing
        Assert.Equal("SPEC_REQUIRED", await Code(await client.PutAsJsonAsync($"/api/scenarios/{id}/enclave", new { spec = (object?)null })));
        Assert.Equal("SCENARIO_UNKNOWN", await Code(await client.GetAsync("/api/scenarios/999/enclave")));
    }

    [Fact]
    public async Task Save_validate_and_plan_send_the_draft_and_record_the_version_and_job()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["PUT /api/v1/slices/harbor-blackout"] = () => Json("""{"slug":"harbor-blackout","version":"sha256:aa","created":true,"findings":[]}""");
        grayspace.Routes["POST /api/v1/slices/harbor-blackout/validate"] = () =>
            Json("""{"valid":false,"version":"sha256:aa","findings":[{"code":"NO_SITES","severity":"warning","path":"sites","message":"no sites"}]}""");
        grayspace.Routes["POST /api/v1/slices/harbor-blackout/plan"] = () => Json("""{"job":{"id":"job-plan","kind":"plan","state":"queued"}}""");
        using var app = With(factory, grayspace);
        var (client, id) = await Ready(app);

        var validated = await Body(await client.PostAsync($"/api/scenarios/{id}/enclave/validate", null));
        var saved = await Body(await client.PostAsync($"/api/scenarios/{id}/enclave/save", null));
        var planned = await Body(await client.PostAsync($"/api/scenarios/{id}/enclave/plan", null));

        Assert.Equal("NO_SITES", (string)validated["graySpace"]!["findings"]![0]!["code"]!);
        Assert.Null(validated["enclave"]!["version"]);
        Assert.Equal("harbor-blackout", (string)grayspace.BodyOf("POST /api/v1/slices/harbor-blackout/validate")["slug"]!);
        Assert.Equal("sha256:aa", (string)saved["enclave"]!["version"]!);
        Assert.Equal("sha256:aa", (string)(await client.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}"))!["deployments"]![0]!["version"]!);
        Assert.Equal("grayspace/v1", (string)grayspace.BodyOf("PUT /api/v1/slices/harbor-blackout")["apiVersion"]!);
        Assert.Equal("sha256:aa", (string)grayspace.BodyOf("POST /api/v1/slices/harbor-blackout/plan")["version"]!);
        Assert.Equal("job-plan", (string)planned["enclave"]!["jobId"]!);
        Assert.All(grayspace.Calls, c => Assert.Equal($"Bearer {Token}", c.Auth));
    }

    [Fact]
    public async Task An_instance_is_created_followed_retried_cancelled_and_destroyed()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["PUT /api/v1/slices/harbor-blackout"] = () => Json("""{"slug":"harbor-blackout","version":"sha256:aa","created":false,"findings":[]}""");
        grayspace.Routes["POST /api/v1/instances"] = () => Json("""{"instance":"harborbl","job":{"id":"job-1","kind":"create","state":"queued"}}""");
        grayspace.Routes["GET /api/v1/instances/harborbl"] = () => Json("""{"id":"harborbl","state":"provisioning"}""");
        grayspace.Routes["GET /api/v1/jobs/job-1"] = () => Json("""{"id":"job-1","state":"failed","stage":"apply"}""");
        grayspace.Routes["GET /api/v1/jobs/job-1/log?follow=false"] = () => new HttpResponseMessage { Content = new StringContent("validate ok\napply failed\n") };
        grayspace.Routes["POST /api/v1/jobs/job-1/retry"] = () => Json("""{"job":{"id":"job-2","kind":"create","state":"queued"}}""");
        grayspace.Routes["POST /api/v1/jobs/job-2/cancel"] = () => Json("""{"id":"job-2","state":"running","cancel_requested":true}""");
        grayspace.Routes["DELETE /api/v1/instances/harborbl"] = () => Json("""{"instance":"harborbl","job":{"id":"job-3","kind":"destroy","state":"queued"}}""");
        using var app = With(factory, grayspace);
        var (client, id) = await Ready(app);
        var url = $"/api/scenarios/{id}/enclave";

        Assert.Equal("NO_INSTANCE", await Code(await client.DeleteAsync($"{url}/instance")));
        Assert.Equal("NO_JOB", await Code(await client.PostAsync($"{url}/job/retry", null)));
        var created = await Body(await client.PostAsJsonAsync($"{url}/instance", new { prefix = "copy2" }));
        var followed = (await client.GetFromJsonAsync<JsonObject>($"{url}/instance"))!;
        var retried = await Body(await client.PostAsync($"{url}/job/retry", null));
        var cancelled = await Body(await client.PostAsync($"{url}/job/cancel", null));
        var destroyed = await Body(await client.DeleteAsync($"{url}/instance"));

        var create = grayspace.BodyOf("POST /api/v1/instances");
        Assert.Equal(("harbor-blackout", "sha256:aa", "copy2"), ((string)create["slice"]!, (string)create["version"]!, (string)create["prefix"]!));
        Assert.Equal(("harborbl", "job-1"), ((string)created["enclave"]!["instanceId"]!, (string)created["enclave"]!["jobId"]!));
        Assert.Equal(("provisioning", "apply"), ((string)followed["instance"]!["state"]!, (string)followed["job"]!["stage"]!));
        Assert.Contains("apply failed", (string)followed["log"]!);
        Assert.Equal("job-2", (string)retried["enclave"]!["jobId"]!);
        Assert.Equal(("job-2", true), ((string)cancelled["enclave"]!["jobId"]!, (bool)cancelled["graySpace"]!["cancel_requested"]!));
        Assert.Equal(("harborbl", "job-3"), ((string)destroyed["enclave"]!["instanceId"]!, (string)destroyed["enclave"]!["jobId"]!));
        Assert.Equal("ACTION_UNKNOWN", await Code(await client.PostAsync($"{url}/job/resume", null)));
    }

    [Fact]
    public async Task A_refused_create_keeps_GraySpace_findings_and_records_the_saved_version()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["PUT /api/v1/slices/harbor-blackout"] = () => Json("""{"slug":"harbor-blackout","version":"sha256:bb","created":true,"findings":[]}""");
        grayspace.Routes["POST /api/v1/instances"] = () =>
            Json("""{"detail":{"findings":[{"code":"IMAGE_UNKNOWN","severity":"error","path":"hosts[0].image","message":"unknown image win-11"}]}}""", HttpStatusCode.UnprocessableEntity);
        using var app = With(factory, grayspace);
        var (client, id) = await Ready(app);

        var refused = await client.PostAsJsonAsync($"/api/scenarios/{id}/enclave/instance", new { });
        var body = await Body(refused);
        var enclave = (await client.GetFromJsonAsync<JsonObject>($"/api/scenarios/{id}/enclave"))!;

        Assert.Equal((HttpStatusCode.UnprocessableEntity, "GRAYSPACE_REFUSED"), (refused.StatusCode, (string)body["code"]!));
        Assert.Contains("hosts[0].image: unknown image win-11", (string)body["message"]!);
        Assert.Equal("IMAGE_UNKNOWN", (string)body["detail"]!["findings"]![0]!["code"]!);
        Assert.Equal("sha256:bb", (string)enclave["version"]!);
        Assert.Null(enclave["instanceId"]);
    }

    [Fact]
    public async Task An_admin_checks_deploys_configures_and_destroys_the_base_and_follows_its_job()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["GET /api/v1/base/versions"] = () => Json("""[{"name":"grayspace/2026.10","csv_sha256":"aa","deployed":false,"summary":null}]""");
        grayspace.Routes["GET /api/v1/base/preflight?version=grayspace%2F2026.10&scope=tier1"] = () =>
            Json("""{"ready":true,"version":"grayspace/2026.10","scope":"tier1","checks":[{"name":"proxmox-api","status":"ok","message":"reachable"}]}""");
        grayspace.Routes["POST /api/v1/base/deploy"] = () => Json("""{"job":{"id":"job-b1","kind":"deploy_base","target":"base","state":"queued"}}""", HttpStatusCode.Accepted);
        grayspace.Routes["POST /api/v1/base/configure"] = () => Json("""{"job":{"id":"job-b2","kind":"configure_base","target":"base","state":"queued"}}""", HttpStatusCode.Accepted);
        grayspace.Routes["GET /api/v1/jobs?target=base&limit=1"] = () => Json("""[{"id":"job-b2","kind":"configure_base","target":"base","state":"failed","stage":"ansible"}]""");
        grayspace.Routes["GET /api/v1/jobs/job-b2/log?follow=false"] = () => new HttpResponseMessage { Content = new StringContent("ansible failed\n") };
        grayspace.Routes["POST /api/v1/jobs/job-b2/retry"] = () => Json("""{"job":{"id":"job-b3","kind":"configure_base","target":"base","state":"queued"}}""", HttpStatusCode.Accepted);
        grayspace.Routes["DELETE /api/v1/base"] = () => Json("""[{"id":"job-b4","kind":"destroy_base","target":"base","state":"queued"}]""", HttpStatusCode.Accepted);
        using var app = With(factory, grayspace);
        var (client, _) = await Ready(app);

        var offers = (await client.GetFromJsonAsync<JsonObject>("/api/grayspace/offers"))!;
        var preflight = (await client.GetFromJsonAsync<JsonObject>("/api/grayspace/base/preflight?version=grayspace/2026.10&scope=tier1"))!;
        var deployed = await Body(await client.PostAsJsonAsync("/api/grayspace/base/deploy", new { version = "grayspace/2026.10", scope = "tier1" }));
        var configured = await Body(await client.PostAsync("/api/grayspace/base/configure", null));
        var followed = (await client.GetFromJsonAsync<JsonObject>("/api/grayspace/base/job"))!;
        var retried = await Body(await client.PostAsync("/api/grayspace/base/job/retry", null));
        var destroyed = JsonNode.Parse(await (await client.DeleteAsync("/api/grayspace/base")).Content.ReadAsStringAsync())!;

        Assert.Equal("grayspace/2026.10", (string)offers["versions"]![0]!["name"]!);
        Assert.True((bool)preflight["ready"]!);
        Assert.Equal("job-b1", (string)deployed["job"]!["id"]!);
        var deploy = grayspace.BodyOf("POST /api/v1/base/deploy");
        Assert.Equal(("grayspace/2026.10", "tier1"), ((string)deploy["version"]!, (string)deploy["scope"]!));
        Assert.Equal("job-b2", (string)configured["job"]!["id"]!);
        Assert.Equal(("job-b2", "ansible"), ((string)followed["job"]!["id"]!, (string)followed["job"]!["stage"]!));
        Assert.Contains("ansible failed", (string)followed["log"]!);
        Assert.Equal("job-b3", (string)retried["job"]!["id"]!);
        Assert.Equal("job-b4", (string)destroyed[0]!["id"]!);
        Assert.Equal("ACTION_UNKNOWN", await Code(await client.PostAsync("/api/grayspace/base/rebuild", null)));
        Assert.Equal("ACTION_UNKNOWN", await Code(await client.PostAsync("/api/grayspace/base/job/resume", null)));
    }

    [Fact]
    public async Task Only_an_admin_changes_the_base_and_it_never_takes_live_instances_down_with_it()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["DELETE /api/v1/base"] = () =>
            Json("""{"detail":"2 slice instance(s) are live (harborbl, mer); destroy them first, or pass destroy_instances=true"}""", HttpStatusCode.Conflict);
        grayspace.Routes["GET /api/v1/jobs?target=base&limit=1"] = () => Json("[]");
        using var app = With(factory, grayspace);
        var (admin, _) = await Ready(app);
        var lee = Client(app, user: "lee");

        Assert.Equal("NOT_ADMIN", await Code(await lee.GetAsync("/api/grayspace/base/preflight?version=grayspace/2026.10")));
        Assert.Equal("NOT_ADMIN", await Code(await lee.PostAsJsonAsync("/api/grayspace/base/deploy", new { version = "grayspace/2026.10" })));
        Assert.Equal("NOT_ADMIN", await Code(await lee.PostAsync("/api/grayspace/base/configure", null)));
        Assert.Equal("NOT_ADMIN", await Code(await lee.DeleteAsync("/api/grayspace/base")));
        Assert.Equal("NOT_ADMIN", await Code(await lee.PostAsync("/api/grayspace/base/job/cancel", null)));
        Assert.Equal("AGENT_CANNOT_DEPLOY", await Code(await Client(app, agent: true).GetAsync("/api/grayspace/base/job")));
        var none = (await lee.GetFromJsonAsync<JsonObject>("/api/grayspace/base/job"))!;
        Assert.Null(none["job"]);
        Assert.Equal("NO_JOB", await Code(await admin.PostAsync("/api/grayspace/base/job/cancel", null)));

        var refused = await admin.DeleteAsync("/api/grayspace/base");
        var body = await Body(refused);

        Assert.Equal((HttpStatusCode.Conflict, "GRAYSPACE_REFUSED"), (refused.StatusCode, (string)body["code"]!));
        Assert.Contains("harborbl, mer", (string)body["message"]!);
        Assert.DoesNotContain(grayspace.Calls, c => c.Route.Contains("destroy_instances"));
    }

    [Fact]
    public async Task Reusing_an_enclave_on_GraySpace_makes_its_spec_the_draft()
    {
        using var factory = new ApiFactory();
        var grayspace = new FakeGraySpace();
        grayspace.Routes["GET /api/v1/slices/meridian"] = () =>
            Json("""{"slug":"meridian","kind":"slice","description":"","version":"sha256:cc","spec":{"apiVersion":"grayspace/v1","kind":"Slice","slug":"meridian","prefix":"mer"}}""");
        grayspace.Routes["GET /api/v1/slices/base"] = () => Json("""{"slug":"base","kind":"base","description":"","spec":null}""");
        using var app = With(factory, grayspace);
        var (client, id) = await Ready(app);

        var reused = await Body(await client.PostAsJsonAsync($"/api/scenarios/{id}/enclave/reuse", new { slug = "meridian" }));

        Assert.Equal(("meridian", "mer", "sha256:cc"), ((string)reused["enclave"]!["spec"]!["slug"]!, (string)reused["enclave"]!["spec"]!["prefix"]!, (string)reused["enclave"]!["version"]!));
        Assert.Equal("ENCLAVE_HAS_NO_SPEC", await Code(await client.PostAsJsonAsync($"/api/scenarios/{id}/enclave/reuse", new { slug = "base" })));
        Assert.Equal("GRAYSPACE_REFUSED", await Code(await client.PostAsJsonAsync($"/api/scenarios/{id}/enclave/reuse", new { slug = "nope" })));
    }
}
