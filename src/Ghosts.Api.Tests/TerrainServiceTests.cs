// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Ghosts.Api.Infrastructure.Terrain;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// An execution's terrain: GHOSTS asks the provider to build the scenario's enclave on its base, keyed by the
/// execution so a retry is the same build, follows the build until it is ready, keeps the manifest, and tears down.
/// </summary>
public class TerrainServiceTests
{
    private readonly Mock<ITerrainProvider> _provider = new();
    private CreateTerrainInstance? _requested;

    public TerrainServiceTests()
    {
        _provider.Setup(p => p.ListEnclavesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TerrainEnclave("office-enclave", "1", "office-base", 5)]);
        _provider.Setup(p => p.CreateInstanceAsync(It.IsAny<CreateTerrainInstance>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTerrainInstance, CancellationToken>((r, _) => _requested = r)
            .ReturnsAsync(Instance("inst-1", TerrainStatus.Queued, 0));
    }

    [Fact]
    public async Task Building_requests_the_scenarios_enclave_on_its_base_with_the_documents_hosts_keyed_by_the_execution()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);

        var terrain = await Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.NotNull(_requested);
        Assert.Equal("office-base", _requested.BaseId);
        Assert.Equal("office-enclave", _requested.Enclave);
        Assert.Equal($"execution-{execution.Id}", _requested.IdempotencyKey);
        Assert.Equal(["desk-01", "desk-02"], _requested.Expected!.Hosts!.Select(h => h.Name));
        Assert.Equal("Office LAN", Assert.Single(_requested.Expected.Segments!).Name);
        Assert.Equal("inst-1", terrain.InstanceId);
        Assert.Equal(TerrainStatus.Queued, terrain.Status);
        Assert.Equal(TerrainStatus.Queued, context.Executions.Single(e => e.Id == execution.Id).TerrainStatus);
    }

    [Fact]
    public async Task A_base_and_enclave_named_in_the_request_are_used_instead()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);

        await Service(context).BuildAsync(execution.Id, new BuildTerrainDto("other-base", "other-enclave"), default);

        Assert.Equal("other-base", _requested!.BaseId);
        Assert.Equal("other-enclave", _requested.Enclave);
    }

    [Fact]
    public async Task A_1_2_0_reference_names_the_enclave_and_base_so_the_provider_is_not_asked_for_the_base()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context, """{"terrain":{"reference":{"provider":"grayspace","enclave":"desk-enclave","base":"desk-base"}}}""");

        await Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal("desk-base", _requested!.BaseId);
        Assert.Equal("desk-enclave", _requested.Enclave);
        _provider.Verify(p => p.ListEnclavesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_second_build_while_one_is_in_progress_is_refused()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default));
        _provider.Verify(p => p.CreateInstanceAsync(It.IsAny<CreateTerrainInstance>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task A_scenario_with_no_enclave_cannot_be_built()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context, extras: null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default));
        Assert.Contains("enclave", error.Message);
    }

    [Fact]
    public async Task Polling_follows_a_build_and_keeps_the_manifest_once_it_is_ready()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _provider.SetupSequence(p => p.GetInstanceAsync("inst-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Instance("inst-1", TerrainStatus.Building, 40))
            .ReturnsAsync(Instance("inst-1", TerrainStatus.Ready, 100));
        _provider.Setup(p => p.GetManifestAsync("inst-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TerrainManifest("inst-1", "office-base", "office-enclave", "office.local",
                [new TerrainManifestHost("desk-01", "Office LAN", "10.20.0.10", "172.30.0.10", "linux", "ubuntu", "workstation", "pve-01", 1001, true, false)], []));

        await service.PollAsync(default);
        var building = await service.GetAsync(execution.Id, default);
        await service.PollAsync(default);
        var ready = await service.GetAsync(execution.Id, default);

        Assert.Equal(TerrainStatus.Building, building.Status);
        Assert.Equal(40, building.Progress);
        Assert.Null(building.Manifest);
        Assert.Equal(TerrainStatus.Ready, ready.Status);
        Assert.Equal("desk-01", Assert.Single(ready.Manifest!.Hosts).Name);
        Assert.Contains(context.ExecutionEvents, e => e.ExecutionId == execution.Id && e.EventType == "TerrainReady");
    }

    [Fact]
    public async Task Tearing_down_asks_the_provider_to_delete_the_instance()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _provider.Setup(p => p.DeleteInstanceAsync("inst-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Instance("inst-1", TerrainStatus.TearingDown, 0));

        var terrain = await service.TeardownAsync(execution.Id, default);

        Assert.Equal(TerrainStatus.TearingDown, terrain.Status);
        _provider.Verify(p => p.DeleteInstanceAsync("inst-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(TerrainStatus.Deleted)]
    [InlineData(TerrainStatus.Failed)]
    public async Task A_rebuild_after_a_teardown_or_a_failure_asks_for_a_new_instance(string ended)
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        var firstKey = _requested!.IdempotencyKey;
        var tracked = context.Executions.Single(e => e.Id == execution.Id);
        tracked.TerrainStatus = ended;
        tracked.Terrain = tracked.Terrain!.Replace("\"status\":\"queued\"", $"\"status\":\"{ended}\"");
        await context.SaveChangesAsync();

        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.NotEqual(firstKey, _requested!.IdempotencyKey);
        Assert.Equal($"execution-{execution.Id}-after-inst-1", _requested.IdempotencyKey);
    }

    [Fact]
    public async Task A_poll_that_races_a_teardown_does_not_put_the_terrain_back_to_ready()
    {
        var db = $"terrain-{Guid.NewGuid()}";
        await using var context = TestDb.Named(db);
        var execution = await Execution(context);
        await Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _provider.Setup(p => p.DeleteInstanceAsync("inst-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Instance("inst-1", TerrainStatus.TearingDown, 0));
        _provider.Setup(p => p.GetManifestAsync("inst-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TerrainManifest("inst-1", "office-base", "office-enclave", "office.local", [], []));
        _provider.Setup(p => p.GetInstanceAsync("inst-1", It.IsAny<CancellationToken>())).Returns(async () =>
        {
            await using var other = TestDb.Named(db);
            await new TerrainService(other, _provider.Object).TeardownAsync(execution.Id, default);
            return Instance("inst-1", TerrainStatus.Ready, 100);
        });

        await Service(context).PollAsync(default);

        await using var check = TestDb.Named(db);
        Assert.Equal(TerrainStatus.TearingDown, (await new TerrainService(check, _provider.Object).GetAsync(execution.Id, default)).Status);
    }

    [Fact]
    public async Task A_player_view_lists_the_ready_manifests_participant_consoles()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var manifest = new TerrainManifest("inst-1", "office-base", "office-enclave", "office.local",
        [
            new TerrainManifestHost("console-01", "Office LAN", "10.20.0.50", "172.30.0.50", "windows", "11", "participant console", "pve-01", 1050, false, true),
            new TerrainManifestHost("desk-01", "Office LAN", "10.20.0.11", "172.30.0.11", "linux", "ubuntu", "workstation", "pve-01", 1011, true, false)
        ], []);
        execution.TerrainStatus = TerrainStatus.Ready;
        execution.Terrain = JsonSerializer.Serialize(new ExecutionTerrainDto(execution.Id, "grayspace", "office-base", "office-enclave",
            "inst-1", TerrainStatus.Ready, 100, [], manifest, null, DateTime.UtcNow), GraySpaceTerrainProvider.Json);
        await context.SaveChangesAsync();

        var view = await Service(context).PlayerViewAsync(execution.Id, default);

        Assert.Equal("office-r1", view.Name);
        var console = Assert.Single(view.Consoles);
        Assert.Equal(("console-01", "10.20.0.50", "pve-01", 1050), (console.Name, console.RangeIp, console.ProxmoxNode, console.Vmid));
    }

    [Fact]
    public async Task There_is_no_player_view_before_the_terrain_is_ready()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(context).PlayerViewAsync(execution.Id, default));
    }

    private TerrainService Service(ApplicationDbContext context) => new(context, _provider.Object);

    private static TerrainInstance Instance(string id, string status, int progress) =>
        new(id, "office-base", "office-enclave", status, progress, "job-1", [status], DateTime.UtcNow, DateTime.UtcNow);

    private const string OfficeTerrain = """
        {"terrain":{"reference":{"provider":"grayspace","slice":"office-enclave"},
                    "segments":[{"name":"Office LAN","cidr":"10.20.0.0/24"}],
                    "hosts":[{"name":"desk-01","segment":"Office LAN","os":"ubuntu","role":"workstation"},
                             {"name":"desk-02","segment":"Office LAN","os":"ubuntu","role":"workstation"}]}}
        """;

    private static async Task<Execution> Execution(ApplicationDbContext context, string? extras = OfficeTerrain)
    {
        var scenario = new Scenario { Name = "Office Day", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Extras = extras };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        var execution = new Execution { ScenarioId = scenario.Id, Name = "office-r1", CreatedAt = DateTime.UtcNow };
        context.Executions.Add(execution);
        await context.SaveChangesAsync();
        return execution;
    }
}
