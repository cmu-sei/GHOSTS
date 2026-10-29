// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Ghosts.Api.Infrastructure.Terrain;

namespace Ghosts.Api.Tests;

/// <summary>
/// The in-memory GraySpace answers the Terrain API 1.3.0 calls GHOSTS makes in the shapes GraySpace publishes: a stored
/// enclave builds through its job's stages to a manifest, findings name what is wrong, teardown frees the prefix, and
/// state changes are published as /api/v1/events messages.
/// </summary>
public class GraySpaceSimulatorTests
{
    private DateTime _now = new(2026, 10, 29, 9, 0, 0, DateTimeKind.Utc);
    private readonly GraySpaceSimulator _sim;

    public GraySpaceSimulatorTests() => _sim = new GraySpaceSimulator(() => _now, buildSeconds: 30);

    private static SliceSpec Office(string? image = null, string? role = null, Dictionary<string, Dictionary<string, string>>? roleVars = null) => new(
        "office-day", "od", "An ordinary day at the office.", "ghosts",
        [new SliceSite("office", new SliceAttach(Isp: "Lumen"), [new SliceSegment("office-lan", "192.168.10.0/24"), new SliceSegment("front-desk")])],
        [
            new SliceHost("staff", image ?? "ubuntu-22.04-lxc", Count: 2, Segment: "office-lan", Roles: [role ?? "ghosts-client"], Labels: new() { ["ghosts.pool"] = "office staff" }),
            new SliceHost("lobby", "ubuntu-22.04-lxc", Segment: "front-desk", Roles: ["ghosts-client"], Labels: new() { ["ghosts.pool"] = "reception" }),
            new SliceHost("console", "ubuntu-22.04-lxc", Segment: "office-lan", Labels: new() { ["ghosts.console"] = "true" })
        ],
        roleVars ?? new() { ["ghosts-client"] = new() { ["client_url"] = "http://ghosts:5000/client.zip", ["api_url"] = "http://ghosts:5000/api" } });

    [Fact]
    public async Task Every_answer_has_the_shape_graySpace_publishes()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        var accepted = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        var pending = await _sim.GetInstanceAsync("g7", default);
        _now = _now.AddSeconds(31);
        var ready = await _sim.GetInstanceAsync("g7", default);

        Assert.Empty(Check("DeployedBase", await _sim.GetBaseAsync(default)));
        Assert.Empty(Check("Capabilities", await _sim.GetCapabilitiesAsync(default)));
        Assert.Empty(Check("PutSliceResult", put));
        Assert.Empty(Check("ValidateResult", await _sim.ValidateSliceAsync(Office(), default)));
        Assert.Empty(Check("JobAccepted", accepted));
        Assert.Empty(Check("JobRecord", await _sim.GetJobAsync(accepted.Job.Id, default)));
        Assert.Empty(Check("InstanceView", pending));
        Assert.Empty(Check("InstanceView", ready));
        Assert.Empty(GraySpaceContract.Errors("FeatureCollection", await _sim.GetMapAsync("g7", default)));
        foreach (var item in (await _sim.ListSlicesAsync(default)).Select(s => Check("SliceSummary", s))) Assert.Empty(item);
    }

    [Fact]
    public async Task A_stored_enclave_builds_through_its_jobs_stages_to_a_manifest()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        var accepted = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        _now = _now.AddSeconds(15);
        var midway = await _sim.GetJobAsync(accepted.Job.Id, default);
        var building = await _sim.GetInstanceAsync("g7", default);
        _now = _now.AddSeconds(16);
        var job = await _sim.GetJobAsync(accepted.Job.Id, default);
        var instance = await _sim.GetInstanceAsync("g7", default);

        Assert.Equal(("g7", JobState.Queued, "create_instance"), (accepted.Instance, accepted.Job.State, accepted.Job.Kind));
        Assert.Equal(JobState.Running, midway.State);
        Assert.NotNull(midway.Stage);
        Assert.Contains(building.State, new[] { InstanceState.Provisioning, InstanceState.Configuring });
        Assert.Equal((JobState.Succeeded, InstanceState.Ready), (job.State, instance.State));
        Assert.Equal(["g7-staff-01", "g7-staff-02", "g7-lobby", "g7-console"], instance.Hosts.Select(h => h.Hostname));
        Assert.Equal("office staff", instance.Hosts[0].Labels["ghosts.pool"]);
        Assert.Equal(("office-lan", "192.168.10.10"), (instance.Hosts[0].Segment, instance.Hosts[0].Ip));
        Assert.Equal("g7-office-edge", Assert.Single(instance.Sites).Edge);
        Assert.Contains("ready", await _sim.GetJobLogAsync(accepted.Job.Id, default));
    }

    [Theory]
    [InlineData("windows-11", null, "UNKNOWN_IMAGE", "hosts[0].image")]
    [InlineData(null, "kiosk", "UNKNOWN_ROLE", "hosts[0].roles[0]")]
    [InlineData("ghosts-pandora", "ghosts-client", "INCOMPATIBLE_ROLE", "hosts[0].roles[0]")]
    public async Task Findings_name_what_graySpace_cannot_build_and_where(string? image, string? role, string code, string path)
    {
        var result = await _sim.ValidateSliceAsync(Office(image, role), default);

        Assert.False(result.Valid);
        var finding = Assert.Single(result.Findings, f => f.Code == code);
        Assert.Equal((path, "error"), (finding.Path, finding.Severity));
    }

    [Fact]
    public async Task A_ghosts_client_without_its_role_variables_is_a_finding()
    {
        var result = await _sim.ValidateSliceAsync(Office(roleVars: []), default);

        Assert.Contains(result.Findings, f => f.Code == "MISSING_ROLE_VAR" && f.Severity == "error");
    }

    [Fact]
    public async Task An_enclave_with_errors_is_stored_but_an_instance_of_it_is_refused_with_its_findings()
    {
        var put = await _sim.PutSliceAsync(Office(image: "windows-11"), default);

        var refused = await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default));

        Assert.Contains(put.Findings, f => f.Code == "UNKNOWN_IMAGE");
        Assert.Equal(422, refused.Status);
        Assert.Contains(refused.Findings!, f => f.Code == "UNKNOWN_IMAGE");
        Assert.Equal(404, (await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.GetInstanceAsync("g7", default))).Status);
    }

    [Fact]
    public async Task A_site_attached_nowhere_is_refused_as_graySpace_refuses_it()
    {
        var spec = Office() with { Sites = [new SliceSite("office", new SliceAttach(), [new SliceSegment("office-lan"), new SliceSegment("front-desk")])] };

        var put = await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.PutSliceAsync(spec, default));
        var validate = await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.ValidateSliceAsync(spec, default));

        Assert.Equal((422, 422), (put.Status, validate.Status));
        Assert.Contains("give an isp, a location, or a router", put.Detail);
    }

    [Fact]
    public async Task Jobs_run_one_at_a_time_and_an_instance_waits_pending_behind_another()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        var second = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g8"), default);
        _now = _now.AddSeconds(31);

        var waiting = (await _sim.GetJobAsync(second.Job.Id, default), await _sim.GetInstanceAsync("g8", default));

        Assert.Equal(InstanceState.Ready, (await _sim.GetInstanceAsync("g7", default)).State);
        Assert.Equal((JobState.Running, InstanceState.Provisioning), (waiting.Item1.State, waiting.Item2.State));
        _now = _now.AddSeconds(31);
        Assert.Equal(InstanceState.Ready, (await _sim.GetInstanceAsync("g8", default)).State);
    }

    [Fact]
    public async Task A_destroy_queued_behind_another_job_leaves_the_instance_as_it_was_until_it_starts()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        _now = _now.AddSeconds(31);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g8"), default);

        var destroy = await _sim.DestroyInstanceAsync("g7", default);

        Assert.Equal(JobState.Queued, (await _sim.GetJobAsync(destroy.Job.Id, default)).State);
        Assert.Equal(InstanceState.Ready, (await _sim.GetInstanceAsync("g7", default)).State);
        _now = _now.AddSeconds(31);
        Assert.Equal(InstanceState.Destroying, (await _sim.GetInstanceAsync("g7", default)).State);
        _now = _now.AddSeconds(6);
        Assert.Equal(InstanceState.Destroyed, (await _sim.GetInstanceAsync("g7", default)).State);
    }

    [Fact]
    public async Task Destroying_during_a_build_cancels_it_and_the_instance_is_failed_until_the_destroy_starts()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        var build = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g8"), default);
        _now = _now.AddSeconds(10);

        var destroy = await _sim.DestroyInstanceAsync("g7", default);

        Assert.Equal(JobState.Cancelled, (await _sim.GetJobAsync(build.Job.Id, default)).State);
        Assert.Equal(JobState.Queued, (await _sim.GetJobAsync(destroy.Job.Id, default)).State);
        Assert.Equal(InstanceState.Failed, (await _sim.GetInstanceAsync("g7", default)).State);
    }

    [Fact]
    public async Task A_job_can_be_made_to_fail_at_a_stage_and_its_retry_waits_its_turn()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        _sim.FailNext("create_instance", "apply", "terraform timed out");
        var failing = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        _now = _now.AddSeconds(31);
        var failed = await _sim.GetJobAsync(failing.Job.Id, default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g8"), default);

        var retry = await _sim.RetryJobAsync(failing.Job.Id, default);

        Assert.Equal((JobState.Failed, "apply"), (failed.State, failed.Stage));
        Assert.Contains("terraform timed out", failed.Error);
        Assert.Equal(JobState.Queued, (await _sim.GetJobAsync(retry.Job.Id, default)).State);
        Assert.Equal(InstanceState.Failed, (await _sim.GetInstanceAsync("g7", default)).State);
        _now = _now.AddSeconds(62);
        Assert.Equal(InstanceState.Ready, (await _sim.GetInstanceAsync("g7", default)).State);
    }

    [Fact]
    public async Task Counted_hosts_are_numbered_as_wide_as_their_count_and_teardown_uses_graySpaces_stages()
    {
        var spec = Office() with { Hosts = [new SliceHost("staff", "ubuntu-22.04-lxc", Count: 120, Segment: "office-lan", Roles: ["ghosts-client"])] };
        var put = await _sim.PutSliceAsync(spec with { Sites = [new SliceSite("office", new SliceAttach(Isp: "Lumen"), [new SliceSegment("office-lan", "192.168.10.0/23")])] }, default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        _now = _now.AddSeconds(31);

        var instance = await _sim.GetInstanceAsync("g7", default);
        var destroy = await _sim.DestroyInstanceAsync("g7", default);

        Assert.Equal("g7-staff-001", instance.Hosts[0].Hostname);
        Assert.Equal(["revoke", "dns", "conditions", "detach", "destroy", "workspace", "images", "free"], destroy.Job.Stages);
    }

    [Fact]
    public async Task A_domain_controller_needs_its_realm_domain_and_password()
    {
        var spec = Office() with { Hosts = [new SliceHost("dc01", "ubuntu-24.04-vm", Segment: "office-lan", Roles: ["ad-dc"])] };

        var result = await _sim.ValidateSliceAsync(spec, default);

        Assert.Equal(["role_vars.ad-dc.admin_password", "role_vars.ad-dc.domain", "role_vars.ad-dc.realm"],
            result.Findings.Where(f => f.Code == "MISSING_ROLE_VAR").Select(f => f.Path).Order());
    }

    [Fact]
    public async Task Tearing_down_destroys_the_instance_and_frees_its_prefix()
    {
        var put = await _sim.PutSliceAsync(Office(), default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        _now = _now.AddSeconds(31);

        var destroy = await _sim.DestroyInstanceAsync("g7", default);
        var taken = await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default));
        _now = _now.AddSeconds(6);
        var destroyed = await _sim.GetInstanceAsync("g7", default);
        var again = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);

        Assert.Equal("destroy_instance", destroy.Job.Kind);
        Assert.Equal(409, taken.Status);
        Assert.Equal(InstanceState.Destroyed, destroyed.State);
        Assert.Equal(JobState.Queued, again.Job.State);
    }

    [Fact]
    public async Task A_failed_job_can_be_retried_and_a_succeeded_one_cannot()
    {
        var put0 = await _sim.PutSliceAsync(Office(), default);
        _sim.FailNext("create_instance", "validate", "capacity changed");
        var failed = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put0.Version, "g7"), default);
        _now = _now.AddSeconds(31);

        var retried = await _sim.RetryJobAsync(failed.Job.Id, default);
        var ok = await _sim.PutSliceAsync(Office(), default);
        var built = await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", ok.Version, "g8"), default);
        _now = _now.AddSeconds(62);
        var refused = await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.RetryJobAsync(built.Job.Id, default));

        Assert.Equal(failed.Job.Id, retried.Job.RetryOf);
        Assert.Equal(409, refused.Status);
    }

    [Fact]
    public async Task Unknown_things_are_not_found()
    {
        Assert.Equal(404, (await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.GetInstanceAsync("nope", default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.GetJobAsync("nope", default))).Status);
        Assert.Equal(404, (await Assert.ThrowsAsync<TerrainProviderException>(() => _sim.CreateInstanceAsync(new CreateInstanceRequest("nope"), default))).Status);
    }

    [Fact]
    public async Task State_changes_are_published_as_graySpace_events()
    {
        var events = _sim.Subscribe();
        var put = await _sim.PutSliceAsync(Office(), default);
        await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", put.Version, "g7"), default);
        _sim.Tick();
        _now = _now.AddSeconds(31);
        _sim.Tick();

        var messages = new List<string>();
        while (events.TryRead(out var message)) messages.Add(message);
        var types = messages.Select(m => JsonDocument.Parse(m).RootElement.GetProperty("type").GetString()).ToList();

        Assert.All(messages, m => Assert.Empty(GraySpaceContract.EventErrors(m)));
        Assert.Contains("job.update", types);
        Assert.Contains("instance.update", types);
        Assert.Contains(messages, m => m.Contains("\"type\":\"instance.update\"") && m.Contains("\"state\":\"ready\""));
        Assert.Contains(messages, m => m.Contains("\"type\":\"feature.update\"") && m.Contains("\"id\":\"g7/office\""));
    }

    private static List<string> Check<T>(string component, T value) =>
        GraySpaceContract.Errors(component, JsonSerializer.Serialize(value, GraySpaceClient.Wire));
}
