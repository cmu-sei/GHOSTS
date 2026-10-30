// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using static ScenarioBuilder.Api.Tests.TestApi;

namespace ScenarioBuilder.Api.Tests;

public class PluginTests
{
    private static async Task<JsonObject[]> Plugins(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonArray>("/api/plugins"))!.Select(p => p!.AsObject()).ToArray();

    private static Task<HttpResponseMessage> Connect(HttpClient client, string plugin, string url) =>
        client.PutAsJsonAsync($"/api/plugins/{plugin}", new { url });

    private static Task<HttpResponseMessage> Send(HttpClient client, int id, string plugin) =>
        client.PostAsync($"/api/scenarios/{id}/send/{plugin}", null);

    [Fact]
    public async Task Every_app_is_listed_unconnected_at_first()
    {
        using var factory = new ApiFactory();

        var plugins = await Plugins(factory.Client(user: "lee"));

        Assert.Equal(["blueprint", "cite", "gallery", "player", "steamfitter", "ghosts"], plugins.Select(p => (string)p["id"]!));
        Assert.Equal(["Blueprint", "CITE", "Gallery", "Player", "Steamfitter", "GHOSTS"], plugins.Select(p => (string)p["name"]!));
        Assert.All(plugins, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace((string)p["description"]!));
            Assert.False((bool)p["connected"]!);
            Assert.Null(p["url"]);
        });
    }

    [Fact]
    public async Task An_admin_connects_a_plugin_by_its_api_url()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();

        var connected = await Connect(admin, "blueprint", " https://blueprint.range.local/api ");

        Assert.Equal(HttpStatusCode.OK, connected.StatusCode);
        var blueprint = (await Plugins(factory.Client(user: "lee"))).Single(p => (string)p["id"]! == "blueprint");
        Assert.True((bool)blueprint["connected"]!);
        Assert.Equal("https://blueprint.range.local/api", (string)blueprint["url"]!);
        Assert.Equal("dev", (string)blueprint["connectedBy"]!);
    }

    [Fact]
    public async Task Only_admins_connect_or_disconnect_plugins()
    {
        using var factory = new ApiFactory();

        Assert.Equal("NOT_ADMIN", await Code(await Connect(factory.Client(user: "lee"), "cite", "https://cite.local")));
        Assert.Equal("NOT_ADMIN", await Code(await Connect(factory.Client(agent: true), "cite", "https://cite.local")));
        Assert.Equal("NOT_ADMIN", await Code(await factory.Client(user: "lee").DeleteAsync("/api/plugins/cite")));
        Assert.False((bool)(await Plugins(factory.Client())).Single(p => (string)p["id"]! == "cite")["connected"]!);
    }

    [Fact]
    public async Task A_plugin_needs_a_web_address_and_a_known_name()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();

        var ftp = await Connect(admin, "cite", "ftp://cite.local");
        Assert.Equal(HttpStatusCode.BadRequest, ftp.StatusCode);
        Assert.Equal("URL_INVALID", await Code(ftp));
        Assert.Equal("PLUGIN_UNKNOWN", await Code(await Connect(admin, "nope", "https://x.local")));
    }

    [Fact]
    public async Task Send_waits_for_a_connection_then_says_sending_is_not_built_yet()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateScenario(admin);

        var before = await Send(admin, id, "gallery");
        Assert.Equal(HttpStatusCode.Conflict, before.StatusCode);
        var refusal = await Body(before);
        Assert.Equal("NOT_CONNECTED", (string)refusal["code"]!);
        Assert.Contains("Connect Gallery first", (string)refusal["message"]!);

        await Connect(admin, "gallery", "https://gallery.local/api");
        var after = await Send(admin, id, "gallery");
        Assert.Equal(HttpStatusCode.NotImplemented, after.StatusCode);
        var body = await Body(after);
        Assert.Equal("SEND_NOT_BUILT", (string)body["code"]!);
        Assert.Equal("Gallery is connected at https://gallery.local/api, but sending a scenario to it isn't built yet.", (string)body["message"]!);

        Assert.Equal("TARGET_UNKNOWN", await Code(await Send(admin, id, "nope")));
    }

    [Fact]
    public async Task Disconnecting_puts_a_plugin_back_to_unconnected()
    {
        using var factory = new ApiFactory();
        var admin = factory.Client();
        var id = await CreateScenario(admin);
        await Connect(admin, "ghosts", "http://ghosts.local:5000");

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync("/api/plugins/ghosts")).StatusCode);

        Assert.False((bool)(await Plugins(admin)).Single(p => (string)p["id"]! == "ghosts")["connected"]!);
        Assert.Equal("NOT_CONNECTED", await Code(await Send(admin, id, "ghosts")));
    }
}
