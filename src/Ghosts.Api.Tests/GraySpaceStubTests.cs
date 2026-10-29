// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Terrain;

namespace Ghosts.Api.Tests;

/// <summary>
/// The stub answers the Terrain API the way GraySpace would, in memory: a build moves from queued to building to
/// ready over its build time, its manifest lists the enclave's hosts, and teardown ends in deleted.
/// </summary>
public class GraySpaceStubTests
{
    private DateTime _now = new(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc);

    private GraySpaceStub Stub() => new(() => _now, buildSeconds: 30);

    private static CreateTerrainInstance Request(string key = "exec-1") => new("office-base", "office-enclave", key, "office-r1",
        new TerrainExpectation(
            [new TerrainSegment("Office LAN", "10.20.0.0/24")],
            [new TerrainHost("desk-01", "Office LAN", "ubuntu", "workstation"), new TerrainHost("console-01", "Office LAN", "windows", "participant console")]));

    [Fact]
    public void A_build_is_queued_then_building_then_ready_after_its_build_time()
    {
        var stub = Stub();
        var created = stub.Create(Request());

        Assert.Equal(TerrainStatus.Queued, created.Status);
        _now = _now.AddSeconds(15);
        var halfway = stub.Get(created.Id)!;
        _now = _now.AddSeconds(20);
        var done = stub.Get(created.Id)!;

        Assert.Equal(TerrainStatus.Building, halfway.Status);
        Assert.InRange(halfway.Progress, 40, 60);
        Assert.Equal(TerrainStatus.Ready, done.Status);
        Assert.Equal(100, done.Progress);
    }

    [Fact]
    public void A_ready_instances_manifest_lists_the_expected_hosts_with_placement_and_who_uses_them()
    {
        var stub = Stub();
        var created = stub.Create(Request());
        _now = _now.AddMinutes(1);

        var manifest = stub.Manifest(created.Id).Manifest!;

        Assert.Equal(["console-01", "desk-01"], manifest.Hosts.Select(h => h.Name).Order());
        var desk = manifest.Hosts.Single(h => h.Name == "desk-01");
        Assert.Equal("Office LAN", desk.Segment);
        Assert.Equal("linux", desk.OsFamily);
        Assert.True(desk.GhostsClient);
        Assert.False(desk.ParticipantWorkstation);
        var console = manifest.Hosts.Single(h => h.Name == "console-01");
        Assert.Equal("windows", console.OsFamily);
        Assert.True(console.ParticipantWorkstation);
        Assert.False(console.GhostsClient);
        Assert.Equal(manifest.Hosts.Count, manifest.Hosts.Select(h => h.Vmid).Distinct().Count());
        Assert.StartsWith("10.20.0.", desk.RangeIp);
    }

    [Fact]
    public void The_manifest_is_not_ready_until_the_build_is()
    {
        var stub = Stub();
        var created = stub.Create(Request());

        var result = stub.Manifest(created.Id);

        Assert.Null(result.Manifest);
        Assert.Equal("not_ready", result.Error!.Code);
    }

    [Fact]
    public void Repeating_a_create_with_the_same_idempotency_key_returns_the_same_instance()
    {
        var stub = Stub();

        var first = stub.Create(Request("exec-7"));
        var again = stub.Create(Request("exec-7"));
        var other = stub.Create(Request("exec-8"));

        Assert.Equal(first.Id, again.Id);
        Assert.NotEqual(first.Id, other.Id);
    }

    [Fact]
    public void Teardown_moves_an_instance_to_tearing_down_then_deleted()
    {
        var stub = Stub();
        var created = stub.Create(Request());
        _now = _now.AddMinutes(1);

        var tearing = stub.Delete(created.Id)!;
        _now = _now.AddMinutes(1);

        Assert.Equal(TerrainStatus.TearingDown, tearing.Status);
        Assert.Equal(TerrainStatus.Deleted, stub.Get(created.Id)!.Status);
    }

    [Fact]
    public void Bases_enclaves_and_a_base_snapshot_are_listed()
    {
        var stub = Stub();

        var baseId = Assert.Single(stub.ListBases()).Id;
        var snapshot = stub.Snapshot(baseId)!;

        Assert.Equal("office-base", baseId);
        Assert.NotEmpty(snapshot.Segments);
        Assert.NotEmpty(snapshot.Hosts);
        Assert.Contains(stub.ListEnclaves(), e => e.BaseId == baseId);
        Assert.Null(stub.Snapshot("no-such-base"));
        Assert.Null(stub.Get("no-such-instance"));
    }
}
