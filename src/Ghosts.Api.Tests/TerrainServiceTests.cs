// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.Extensions.Options;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>
/// An execution's terrain through GraySpace 1.3.0: GHOSTS checks the deployed base, stores the scenario's enclave as a
/// slice spec, builds an instance named for the execution, follows its job to a manifest, and tears it down. Findings
/// with errors refuse the build and are kept on the terrain; a failed build is retried rather than re-created.
/// </summary>
public class TerrainServiceTests
{
    private DateTime _now = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private readonly GraySpaceSimulator _sim;
    private static readonly TerrainProviderOptions Settings = new() { ClientUrl = "http://ghosts:5000/client.zip", GhostsApiUrl = "http://ghosts:5000/api" };

    public TerrainServiceTests() => _sim = new GraySpaceSimulator(() => _now, buildSeconds: 30);

    private const string OfficeTerrain = """
        {"terrain":{"reference":{"provider":"grayspace","enclave":"office-enclave","base":"grayspace/2026.10"},
                    "segments":[{"name":"Office LAN","cidr":"10.20.0.0/24"}],
                    "hosts":[{"name":"desk-01","segment":"Office LAN","os":"ubuntu","role":"workstation"},
                             {"name":"console-01","segment":"Office LAN","os":"ubuntu","role":"participant console"}]}}
        """;

    [Fact]
    public async Task Building_stores_the_enclave_and_creates_an_instance_named_for_the_execution()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);

        var terrain = await Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal(("grayspace/2026.10", "office-enclave", $"g{execution.Id}", TerrainStatus.Queued), (terrain.BaseId, terrain.Enclave, terrain.InstanceId, terrain.Status));
        Assert.StartsWith("sha256:", terrain.Version);
        Assert.NotNull(terrain.JobId);
        var stored = Assert.Single(await _sim.ListSlicesAsync(default), s => s.Kind == "slice");
        Assert.Equal(("office-enclave", "ghosts"), (stored.Slug, stored.Owner));
    }

    [Fact]
    public async Task Polling_follows_the_build_to_a_manifest_of_the_hosts_graySpace_named()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        _now = _now.AddSeconds(15);
        await service.PollAsync(default);
        var building = await service.GetAsync(execution.Id, default);
        _now = _now.AddSeconds(16);
        await service.PollAsync(default);
        var ready = await service.GetAsync(execution.Id, default);

        Assert.Equal(TerrainStatus.Building, building.Status);
        Assert.InRange(building.Progress, 1, 99);
        Assert.NotNull(building.Stage);
        Assert.Equal((TerrainStatus.Ready, 100), (ready.Status, ready.Progress));
        Assert.Contains($"g{execution.Id}-desk-01", ready.Manifest!.Hosts.Select(h => h.Hostname));
        Assert.Contains($"g{execution.Id}-office-staff-01", ready.Manifest.Hosts.Select(h => h.Hostname));
        Assert.Contains(ready.Log, l => l.Contains("succeeded"));
        Assert.Contains(context.ExecutionEvents, e => e.ExecutionId == execution.Id && e.EventType == "TerrainReady");
    }

    [Fact]
    public async Task Findings_with_errors_refuse_the_build_and_stay_on_the_terrain()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);

        var terrain = await Service(context, new TerrainProviderOptions()).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal(TerrainStatus.Failed, terrain.Status);
        Assert.Null(terrain.InstanceId);
        Assert.Contains(terrain.Findings!, f => f.Code == "MISSING_ROLE_VAR");
        Assert.Contains(terrain.Notes!, n => n.Contains("TerrainProvider:ClientUrl"));
        Assert.Contains("MISSING_ROLE_VAR", terrain.Error);
    }

    [Theory]
    [InlineData("not_deployed", "grayspace/2026.10", "no base deployed")]
    [InlineData("healthy", "grayspace/2027.01", "grayspace/2027.01")]
    public async Task A_base_that_is_missing_or_not_the_scenarios_refuses_the_build(string status, string deployed, string message)
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context, OfficeTerrain.Replace("grayspace/2026.10", deployed == "grayspace/2027.01" ? "grayspace/2027.01" : "grayspace/2026.10"));
        var graySpace = new Mock<IGraySpace>();
        graySpace.Setup(g => g.GetBaseAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new DeployedBase(
            status == "not_deployed" ? null : GraySpaceSimulator.BaseRef, status, new RouterHealth(0, null), new BgpHealth(null, null)));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TerrainService(context, graySpace.Object, Options.Create(Settings)).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default));

        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task A_graySpace_that_cannot_run_jobs_refuses_the_build_with_its_reason()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var caps = await _sim.GetCapabilitiesAsync(default) with { Jobs = false, JobsUnavailableReason = "no GraySpace checkout here" };
        var graySpace = new Mock<IGraySpace>();
        graySpace.Setup(g => g.GetBaseAsync(It.IsAny<CancellationToken>())).Returns(_sim.GetBaseAsync(default));
        graySpace.Setup(g => g.GetCapabilitiesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(caps);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new TerrainService(context, graySpace.Object, Options.Create(Settings)).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default));

        Assert.Contains("no GraySpace checkout here", ex.Message);
    }

    [Fact]
    public async Task A_second_build_while_one_is_in_progress_is_refused()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default));
    }

    [Fact]
    public async Task Tearing_down_destroys_the_instance_and_a_later_build_makes_a_new_one()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        var first = await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        await service.PollAsync(default);

        var tearing = await service.TeardownAsync(execution.Id, default);
        _now = _now.AddSeconds(6);
        await service.PollAsync(default);
        var deleted = await service.GetAsync(execution.Id, default);
        var again = await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal(TerrainStatus.TearingDown, tearing.Status);
        Assert.Equal(TerrainStatus.Deleted, deleted.Status);
        Assert.Equal(TerrainStatus.Queued, again.Status);
        Assert.NotEqual(first.JobId, again.JobId);
    }

    [Fact]
    public async Task A_failed_build_is_retried_rather_than_created_again()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var graySpace = new FailingFirstBuild(_sim);
        var service = new TerrainService(context, graySpace, Options.Create(Settings));
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        await service.PollAsync(default);
        var failed = await service.GetAsync(execution.Id, default);

        var retried = await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal(TerrainStatus.Failed, failed.Status);
        Assert.Equal(1, graySpace.Creates);
        Assert.Equal(failed.JobId, graySpace.RetriedJob);
        Assert.Equal(TerrainStatus.Queued, retried.Status);
    }

    [Fact]
    public async Task A_poll_that_races_a_teardown_does_not_put_the_terrain_back_to_ready()
    {
        var db = $"terrain-{Guid.NewGuid()}";
        await using var context = TestDb.Named(db);
        var execution = await Execution(context);
        await Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        var graySpace = new Mock<IGraySpace>();
        graySpace.Setup(g => g.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<string, CancellationToken>((id, ct) => _sim.GetJobAsync(id, ct));
        graySpace.Setup(g => g.GetJobLogAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<string, CancellationToken>((id, ct) => _sim.GetJobLogAsync(id, ct));
        graySpace.Setup(g => g.GetInstanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<string, CancellationToken>(async (id, ct) =>
        {
            var ready = await _sim.GetInstanceAsync(id, ct);
            await using var other = TestDb.Named(db);
            await Service(other).TeardownAsync(execution.Id, ct);
            return ready;
        });

        await new TerrainService(context, graySpace.Object, Options.Create(Settings)).PollAsync(default);

        await using var check = TestDb.Named(db);
        Assert.Equal(TerrainStatus.TearingDown, (await Service(check).GetAsync(execution.Id, default)).Status);
    }

    [Fact]
    public async Task A_teardown_waiting_behind_another_build_shows_as_tearing_down_until_it_is_done()
    {
        await using var context = TestDb.New("terrain");
        var first = await Execution(context);
        var second = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(first.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        await service.PollAsync(default);
        await service.BuildAsync(second.Id, new BuildTerrainDto(null, null), default);

        await service.TeardownAsync(first.Id, default);
        await service.PollAsync(default);
        var waiting = (await service.GetAsync(first.Id, default)).Status;
        _now = _now.AddSeconds(40);
        await service.PollAsync(default);

        Assert.Equal(TerrainStatus.TearingDown, waiting);
        Assert.Equal(TerrainStatus.Deleted, (await service.GetAsync(first.Id, default)).Status);
    }

    [Fact]
    public async Task Tearing_down_during_a_build_ends_deleted_not_stuck_failed()
    {
        await using var context = TestDb.New("terrain");
        var first = await Execution(context);
        var second = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(first.Id, new BuildTerrainDto(null, null), default);
        await service.BuildAsync(second.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(10);

        await service.TeardownAsync(first.Id, default);
        await service.PollAsync(default);
        var waiting = (await service.GetAsync(first.Id, default)).Status;
        _now = _now.AddSeconds(60);
        await service.PollAsync(default);

        Assert.Equal(TerrainStatus.TearingDown, waiting);
        Assert.Equal(TerrainStatus.Deleted, (await service.GetAsync(first.Id, default)).Status);
    }

    [Fact]
    public async Task A_retry_waiting_behind_another_build_shows_as_queued_and_reaches_ready()
    {
        await using var context = TestDb.New("terrain");
        var first = await Execution(context);
        var second = await Execution(context);
        var service = Service(context);
        _sim.FailNext("create_instance", "apply", "terraform timed out");
        await service.BuildAsync(first.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        await service.PollAsync(default);
        var failed = (await service.GetAsync(first.Id, default)).Status;
        await service.BuildAsync(second.Id, new BuildTerrainDto(null, null), default);

        await service.BuildAsync(first.Id, new BuildTerrainDto(null, null), default);
        await service.PollAsync(default);
        var waiting = (await service.GetAsync(first.Id, default)).Status;
        _now = _now.AddSeconds(62);
        await service.PollAsync(default);

        Assert.Equal(TerrainStatus.Failed, failed);
        Assert.Equal(TerrainStatus.Queued, waiting);
        Assert.Equal(TerrainStatus.Ready, (await service.GetAsync(first.Id, default)).Status);
    }

    [Fact]
    public async Task After_a_failed_teardown_build_refuses_and_tearing_down_again_works()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        await service.PollAsync(default);
        _sim.FailNext("destroy_instance", "destroy", "terraform destroy failed");
        await service.TeardownAsync(execution.Id, default);
        _now = _now.AddSeconds(6);
        await service.PollAsync(default);
        var failed = (await service.GetAsync(execution.Id, default)).Status;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default));
        var again = await service.TeardownAsync(execution.Id, default);

        Assert.Equal(TerrainStatus.Failed, failed);
        Assert.Contains("tear", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TerrainStatus.TearingDown, again.Status);
    }

    [Fact]
    public async Task Tearing_down_an_instance_graySpace_already_destroyed_marks_it_deleted()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        var built = await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        await service.PollAsync(default);
        await _sim.DestroyInstanceAsync(built.InstanceId!, default);
        _now = _now.AddSeconds(6);

        var terrain = await service.TeardownAsync(execution.Id, default);

        Assert.Equal(TerrainStatus.Deleted, terrain.Status);
    }

    [Fact]
    public async Task An_instance_graySpace_does_not_know_is_marked_deleted_when_polled()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        await Service(context).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        var forgetful = new Mock<IGraySpace>();
        forgetful.Setup(g => g.GetJobAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TerrainProviderException(404, "not_found", "No job 'j-1'"));
        forgetful.Setup(g => g.GetInstanceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TerrainProviderException(404, "not_found", "No instance 'g1'"));

        await new TerrainService(context, forgetful.Object, Options.Create(Settings)).PollAsync(default);

        var terrain = await Service(context).GetAsync(execution.Id, default);
        Assert.Equal(TerrainStatus.Deleted, terrain.Status);
        Assert.Contains("no longer has", terrain.Error);
    }

    [Fact]
    public async Task A_healthy_base_without_a_version_name_still_builds()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context, OfficeTerrain.Replace(",\"base\":\"grayspace/2026.10\"", ""));
        var unnamed = new UnnamedBase(_sim);

        var terrain = await new TerrainService(context, unnamed, Options.Create(Settings)).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal(TerrainStatus.Queued, terrain.Status);
    }

    [Fact]
    public async Task A_create_graySpace_refuses_keeps_its_findings_on_the_terrain()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var refusing = new Mock<IGraySpace>();
        refusing.Setup(g => g.GetBaseAsync(It.IsAny<CancellationToken>())).Returns(_sim.GetBaseAsync(default));
        refusing.Setup(g => g.GetCapabilitiesAsync(It.IsAny<CancellationToken>())).Returns(_sim.GetCapabilitiesAsync(default));
        refusing.Setup(g => g.PutSliceAsync(It.IsAny<SliceSpec>(), It.IsAny<CancellationToken>())).Returns<SliceSpec, CancellationToken>((s, ct) => _sim.PutSliceAsync(s, ct));
        refusing.Setup(g => g.CreateInstanceAsync(It.IsAny<CreateInstanceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TerrainProviderException(422, "invalid_request", "The slice can't be instantiated")
            {
                Findings = [new Finding("CAPACITY", "error", "hosts", "The node lacks 40 GB of storage")]
            });

        var terrain = await new TerrainService(context, refusing.Object, Options.Create(Settings)).BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);

        Assert.Equal(TerrainStatus.Failed, terrain.Status);
        Assert.Contains(terrain.Findings!, f => f.Code == "CAPACITY");
        Assert.Contains("CAPACITY", terrain.Error);
    }

    [Fact]
    public async Task A_player_view_lists_the_participant_consoles_graySpace_built()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);
        await service.PollAsync(default);

        var view = await service.PlayerViewAsync(execution.Id, default);

        Assert.Equal("office-r1", view.Name);
        var console = Assert.Single(view.Consoles);
        Assert.Equal(("console-01", $"g{execution.Id}-console-01", "10.20.0.11"), (console.Name, console.Hostname, console.Ip));
        Assert.NotNull(console.MgmtIp);
    }

    [Fact]
    public async Task There_is_no_player_view_before_the_terrain_is_ready()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service(context).PlayerViewAsync(execution.Id, default));
    }

    [Fact]
    public async Task The_map_is_graySpaces_layer_with_this_executions_enclave()
    {
        await using var context = TestDb.New("terrain");
        var execution = await Execution(context);
        var service = Service(context);
        await service.BuildAsync(execution.Id, new BuildTerrainDto(null, null), default);
        _now = _now.AddSeconds(31);

        var map = await service.MapAsync(execution.Id, default);

        Assert.Empty(GraySpaceContract.Errors("FeatureCollection", map));
        Assert.Contains($"\"id\":\"g{execution.Id}/main\"", map);
    }

    private TerrainService Service(ApplicationDbContext context, TerrainProviderOptions? settings = null) =>
        new(context, _sim, Options.Create(settings ?? Settings));

    private static async Task<Execution> Execution(ApplicationDbContext context, string extras = OfficeTerrain)
    {
        var scenario = new Scenario { Name = "Office Day", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Extras = extras };
        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();
        var parameters = new ScenarioParameters { ScenarioId = scenario.Id };
        parameters.UserPools.Add(new UserPool { Role = "office staff", Count = 2, Extras = """{"segments":["Office LAN"]}""" });
        context.ScenarioParameters.Add(parameters);
        var execution = new Execution { ScenarioId = scenario.Id, Name = "office-r1", CreatedAt = DateTime.UtcNow };
        context.Executions.Add(execution);
        await context.SaveChangesAsync();
        return execution;
    }

    /// <summary>The simulator, with a deployed base whose version GraySpace can't name (its CSVs came from elsewhere).</summary>
    private sealed class UnnamedBase(GraySpaceSimulator sim) : IGraySpace
    {
        public async Task<DeployedBase> GetBaseAsync(CancellationToken ct) => await sim.GetBaseAsync(ct) with { Version = new BaseVersionRef(null!, "c1", "tier1") };
        public Task<GraySpaceCapabilities> GetCapabilitiesAsync(CancellationToken ct) => sim.GetCapabilitiesAsync(ct);
        public Task<List<SliceSummary>> ListSlicesAsync(CancellationToken ct) => sim.ListSlicesAsync(ct);
        public Task<PutSliceResult> PutSliceAsync(SliceSpec spec, CancellationToken ct) => sim.PutSliceAsync(spec, ct);
        public Task<ValidateResult> ValidateSliceAsync(SliceSpec spec, CancellationToken ct) => sim.ValidateSliceAsync(spec, ct);
        public Task<JobAccepted> CreateInstanceAsync(CreateInstanceRequest request, CancellationToken ct) => sim.CreateInstanceAsync(request, ct);
        public Task<InstanceView> GetInstanceAsync(string id, CancellationToken ct) => sim.GetInstanceAsync(id, ct);
        public Task<JobAccepted> DestroyInstanceAsync(string id, CancellationToken ct) => sim.DestroyInstanceAsync(id, ct);
        public Task<JobRecord> GetJobAsync(string id, CancellationToken ct) => sim.GetJobAsync(id, ct);
        public Task<string> GetJobLogAsync(string id, CancellationToken ct) => sim.GetJobLogAsync(id, ct);
        public Task<JobAccepted> RetryJobAsync(string id, CancellationToken ct) => sim.RetryJobAsync(id, ct);
        public Task<string> GetMapAsync(string id, CancellationToken ct) => sim.GetMapAsync(id, ct);
    }

    /// <summary>The simulator, except that the first build's job fails, so a retry can be told from a new build.</summary>
    private sealed class FailingFirstBuild(GraySpaceSimulator sim) : IGraySpace
    {
        public int Creates;
        public string? RetriedJob;
        private string? _failedJob;

        public Task<DeployedBase> GetBaseAsync(CancellationToken ct) => sim.GetBaseAsync(ct);
        public Task<GraySpaceCapabilities> GetCapabilitiesAsync(CancellationToken ct) => sim.GetCapabilitiesAsync(ct);
        public Task<List<SliceSummary>> ListSlicesAsync(CancellationToken ct) => sim.ListSlicesAsync(ct);
        public Task<PutSliceResult> PutSliceAsync(SliceSpec spec, CancellationToken ct) => sim.PutSliceAsync(spec, ct);
        public Task<ValidateResult> ValidateSliceAsync(SliceSpec spec, CancellationToken ct) => sim.ValidateSliceAsync(spec, ct);
        public Task<InstanceView> GetInstanceAsync(string id, CancellationToken ct) => sim.GetInstanceAsync(id, ct);
        public Task<JobAccepted> DestroyInstanceAsync(string id, CancellationToken ct) => sim.DestroyInstanceAsync(id, ct);
        public Task<string> GetJobLogAsync(string id, CancellationToken ct) => sim.GetJobLogAsync(id, ct);
        public Task<string> GetMapAsync(string id, CancellationToken ct) => sim.GetMapAsync(id, ct);

        public async Task<JobAccepted> CreateInstanceAsync(CreateInstanceRequest request, CancellationToken ct)
        {
            Creates++;
            var accepted = await sim.CreateInstanceAsync(request, ct);
            _failedJob = accepted.Job.Id;
            return accepted with { Job = accepted.Job with { State = JobState.Failed, Error = "apply: terraform timed out" } };
        }

        public async Task<JobRecord> GetJobAsync(string id, CancellationToken ct) =>
            id == _failedJob ? (await sim.GetJobAsync(id, ct)) with { State = JobState.Failed, Stage = "apply", Error = "apply: terraform timed out" } : await sim.GetJobAsync(id, ct);

        public Task<JobAccepted> RetryJobAsync(string id, CancellationToken ct)
        {
            RetriedJob = id;
            return Task.FromResult(new JobAccepted(new JobRecord("j-retry", "create_instance", "g1", JobState.Queued, [], DateTime.UtcNow, RetryOf: id), "g1"));
        }
    }
}
