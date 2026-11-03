// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Animator.Models;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Terrain;

namespace Ghosts.Api.Tests;

/// <summary>
/// A run's NPCs are bound to hosts by name, one NPC per host. Auto-binding uses the terrain manifest's client hosts
/// once the terrain is ready, the document's hosts before that, and each pool's placement; participant consoles
/// get no NPC. A binding can be changed by hand, and the run's accounts list says who sits where.
/// </summary>
public class ExecutionBindingTests
{
    private const string OfficeTerrain = """
        {"terrain":{"segments":[{"name":"Office LAN"},{"name":"Front desk"}],
                    "hosts":[{"name":"desk-01","segment":"Office LAN"},{"name":"desk-02","segment":"Office LAN"},
                             {"name":"lobby-01","segment":"Front desk"},{"name":"console-01","segment":"Office LAN","role":"participant console"}]}}
        """;

    [Fact]
    public async Task Auto_binding_puts_each_pools_npcs_on_its_hosts_one_per_host_and_leaves_extras_unbound()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("office staff", """{"hosts":["desk-01","desk-02"]}"""));
        var npcs = AddNpcs(context, execution.Id, "office staff", 3);

        var result = await Service(context).AutoBindAsync(execution.Id, default);

        var bound = context.Npcs.Where(n => n.ExecutionId == execution.Id && n.HostName != null).Select(n => n.HostName).ToList();
        Assert.Equal(["desk-01", "desk-02"], bound.Order());
        Assert.Equal(2, result.Bound);
        Assert.Equal(1, result.Unbound);
    }

    [Fact]
    public async Task A_pool_placed_on_segments_gets_hosts_in_those_segments()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("reception", """{"segments":["Front desk"]}"""));
        AddNpcs(context, execution.Id, "reception", 1);

        await Service(context).AutoBindAsync(execution.Id, default);

        Assert.Equal("lobby-01", context.Npcs.Single(n => n.ExecutionId == execution.Id).HostName);
    }

    [Fact]
    public async Task Participant_consoles_never_get_an_npc()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("office staff", null));
        AddNpcs(context, execution.Id, "office staff", 5);

        await Service(context).AutoBindAsync(execution.Id, default);

        var hosts = context.Npcs.Where(n => n.ExecutionId == execution.Id).Select(n => n.HostName).ToList();
        Assert.DoesNotContain("console-01", hosts);
        Assert.Equal(3, hosts.Count(h => h != null));
    }

    [Fact]
    public async Task Once_the_terrain_is_ready_npcs_sit_at_the_manifests_client_hosts_by_the_hostname_they_report()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("office staff", null));
        await Ready(context, execution,
            Built("staff-01", "office-lan", ["ghosts-client"], ("ghosts.pool", "office staff")),
            Built("file-01", "office-lan", ["file"]),
            Built("console-01", "office-lan", [], ("ghosts.console", "true")));
        AddNpcs(context, execution.Id, "office staff", 2);

        await Service(context).AutoBindAsync(execution.Id, default);

        var hosts = context.Npcs.Where(n => n.ExecutionId == execution.Id).Select(n => n.HostName).ToList();
        Assert.Contains("g1-staff-01", hosts);
        Assert.DoesNotContain("g1-file-01", hosts);
        Assert.DoesNotContain("g1-console-01", hosts);
        Assert.DoesNotContain("desk-01", hosts);
    }

    [Fact]
    public async Task A_pool_takes_the_hosts_graySpace_labelled_with_it()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("office staff", null), ("reception", null));
        await Ready(context, execution,
            Built("staff-01", "office-lan", ["ghosts-client"], ("ghosts.pool", "office staff")),
            Built("desk-01", "office-lan", ["ghosts-client"], ("ghosts.pool", "reception")));
        AddNpcs(context, execution.Id, "reception", 1);
        AddNpcs(context, execution.Id, "office staff", 1);

        await Service(context).AutoBindAsync(execution.Id, default);

        Assert.Equal("g1-desk-01", context.Npcs.Single(n => n.ExecutionId == execution.Id && n.Team == "reception").HostName);
        Assert.Equal("g1-staff-01", context.Npcs.Single(n => n.ExecutionId == execution.Id && n.Team == "office staff").HostName);
    }

    private static async Task Ready(ApplicationDbContext context, Execution execution, params ManifestHost[] hosts)
    {
        execution.TerrainStatus = TerrainStatus.Ready;
        execution.Terrain = JsonSerializer.Serialize(new ExecutionTerrainDto(execution.Id, "grayspace", "grayspace/2026.10", "office-enclave",
            "g1", TerrainStatus.Ready, 100, [], new TerrainManifest([], [.. hosts], [], []), null, DateTime.UtcNow), TerrainService.Stored);
        await context.SaveChangesAsync();
    }

    private static ManifestHost Built(string name, string segment, List<string> roles, params (string Key, string Value)[] labels) =>
        new(name, $"g1-{name}", "main", segment, "192.168.10.10", null, "10.0.1.10", [], roles, labels.ToDictionary(l => l.Key, l => l.Value), "ubuntu-22.04-lxc", "running");

    [Fact]
    public async Task A_binding_can_be_set_and_cleared_by_hand()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("office staff", null));
        var npc = AddNpcs(context, execution.Id, "office staff", 1).Single();
        var service = Service(context);

        await service.SetHostAsync(execution.Id, npc, "desk-02", default);
        var set = context.Npcs.Single(n => n.Id == npc).HostName;
        await service.SetHostAsync(execution.Id, npc, null, default);

        Assert.Equal("desk-02", set);
        Assert.Null(context.Npcs.Single(n => n.Id == npc).HostName);
    }

    [Fact]
    public async Task The_accounts_list_names_each_bound_npc_its_username_and_its_host()
    {
        await using var context = TestDb.New("binding");
        var execution = await Run(context, OfficeTerrain, ("office staff", null));
        var npc = AddNpcs(context, execution.Id, "office staff", 1, email: "dara.okafor@office.local").Single();
        await Service(context).SetHostAsync(execution.Id, npc, "desk-01", default);

        var account = Assert.Single(await Service(context).AccountsAsync(execution.Id, default));

        Assert.Equal("dara.okafor", account.Username);
        Assert.Equal("dara.okafor@office.local", account.Email);
        Assert.Equal("desk-01", account.HostName);
        Assert.Equal("office staff", account.Team);
    }

    private static ExecutionBindingService Service(ApplicationDbContext context) => new(context);

    private static async Task<Execution> Run(ApplicationDbContext context, string extras, params (string Role, string? Placement)[] pools)
    {
        var scenario = new Scenario { Name = "Office Day", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Extras = extras };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        var parameters = new ScenarioParameters { ScenarioId = scenario.Id };
        foreach (var (role, placement) in pools) parameters.UserPools.Add(new UserPool { Role = role, Count = 1, Extras = placement });
        context.ScenarioParameters.Add(parameters);
        var execution = new Execution { ScenarioId = scenario.Id, Name = "office-r1", CreatedAt = DateTime.UtcNow };
        context.Executions.Add(execution);
        await context.SaveChangesAsync();
        return execution;
    }

    private static List<Guid> AddNpcs(ApplicationDbContext context, int executionId, string team, int count, string? email = null)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var npc = new NpcRecord
            {
                Id = Guid.NewGuid(), ExecutionId = executionId, Team = team, CreatedUtc = DateTime.UtcNow.AddSeconds(i),
                NpcProfile = new NpcProfile { Email = email ?? $"npc{i}@office.local", Name = new NameProfile { First = "Npc", Last = $"{i}" } }
            };
            context.Npcs.Add(npc);
            ids.Add(npc.Id);
        }
        context.SaveChanges();
        return ids;
    }
}
