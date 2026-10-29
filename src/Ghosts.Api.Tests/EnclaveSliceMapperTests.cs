// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Terrain;

namespace Ghosts.Api.Tests;

/// <summary>
/// GHOSTS turns a scenario's terrain and population into the slice spec GraySpace builds (GraySpace's
/// ghosts-integration-v2 §6.3): segments into one site, hosts with GraySpace images and roles, pools into GHOSTS-client
/// hosts labelled with their pool, participant consoles labelled and left without a client, and the client role's
/// variables from settings. The spec is valid against GraySpace's published schema.
/// </summary>
public class EnclaveSliceMapperTests
{
    private static readonly GraySpaceCapabilities Caps =
        new GraySpaceSimulator(() => DateTime.UtcNow, 30).GetCapabilitiesAsync(default).Result;

    private static readonly TerrainProviderOptions Settings = new() { ClientUrl = "http://ghosts:5000/client/ghosts-client-universal.zip", GhostsApiUrl = "http://ghosts:5000/api" };

    private static EnclaveInput Office() => new("office-enclave", "Office Day", "An ordinary day at the office.",
        new TerrainExtrasDto(
            Segments: [new SegmentDto("Office LAN", "10.20.0.0/24"), new SegmentDto("Front desk")],
            Hosts:
            [
                new HostDto("desk-01", "Office LAN", "ubuntu", "workstation"),
                new HostDto("file-01", "Office LAN", "ubuntu", "file server", Services: ["smb"]),
                new HostDto("web-01", "Front desk", "ubuntu", "workstation"),
                new HostDto("console-01", "Office LAN", "windows 11", "participant console")
            ],
            Services: [new TerrainServiceDto("intranet web", Hosts: ["web-01"])]),
        [new PoolPlacement("office staff", 3, ["Office LAN"], []), new PoolPlacement("reception", 1, [], ["desk-01"])]);

    [Fact]
    public async Task The_spec_is_valid_against_graySpaces_schema_and_its_simulator_finds_nothing_wrong()
    {
        var spec = EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec;

        Assert.Empty(GraySpaceContract.SliceErrors(JsonSerializer.Serialize(spec, GraySpaceClient.Wire)));
        Assert.Empty((await new GraySpaceSimulator(() => DateTime.UtcNow, 30).ValidateSliceAsync(spec, default)).Findings);
    }

    [Fact]
    public void A_pool_without_placed_hosts_becomes_ghosts_client_hosts_on_its_segment_labelled_with_the_pool()
    {
        var host = Host(EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec, "office-staff");

        Assert.Equal((3, "office-lan", "ubuntu-22.04-lxc"), (host.Count, host.Segment, host.Image));
        Assert.Equal(["ghosts-client"], host.Roles);
        Assert.Equal("office staff", host.Labels!["ghosts.pool"]);
    }

    [Fact]
    public void A_pool_placed_on_hosts_makes_those_hosts_its_ghosts_clients()
    {
        var desk = Host(EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec, "desk-01");

        Assert.Contains("ghosts-client", desk.Roles!);
        Assert.Equal("reception", desk.Labels!["ghosts.pool"]);
    }

    [Fact]
    public void A_participant_console_is_labelled_and_runs_no_ghosts_client()
    {
        var console = Host(EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec, "console-01");

        Assert.Equal("true", console.Labels!["ghosts.console"]);
        Assert.DoesNotContain("ghosts-client", console.Roles ?? []);
    }

    [Fact]
    public void A_hosts_role_and_services_become_graySpace_roles()
    {
        var spec = EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec;

        Assert.Equal(["file"], Host(spec, "file-01").Roles);
        Assert.Equal(["web"], Host(spec, "web-01").Roles);
        Assert.DoesNotContain(Host(spec, "desk-01").Roles!, r => r != "ghosts-client");
    }

    [Fact]
    public void An_os_graySpace_has_no_image_for_falls_back_with_a_note()
    {
        var mapping = EnclaveSliceMapper.Map(Office(), Caps, Settings);

        Assert.Equal("ubuntu-22.04-lxc", Host(mapping.Spec, "console-01").Image);
        Assert.Contains(mapping.Notes, n => n.Contains("console-01") && n.Contains("windows"));
    }

    [Fact]
    public void Names_become_graySpace_names_and_only_private_cidrs_are_kept()
    {
        var input = Office() with { Terrain = Office().Terrain with { Segments = [new SegmentDto("Office LAN", "10.20.0.0/24"), new SegmentDto("Front desk", "203.0.113.0/24")] } };

        var mapping = EnclaveSliceMapper.Map(input, Caps, Settings);

        var site = Assert.Single(mapping.Spec.Sites);
        Assert.Equal([("office-lan", "10.20.0.0/24"), ("front-desk", null)], site.Segments.Select(s => (s.Name, (string?)s.Cidr)));
        Assert.Contains(mapping.Notes, n => n.Contains("203.0.113.0/24"));
        Assert.Equal(("office-enclave", "office"), (mapping.Spec.Slug, mapping.Spec.Prefix));
        Assert.Equal("ghosts", mapping.Spec.Owner);
    }

    [Fact]
    public void The_site_attaches_to_an_isp_graySpace_has_so_graySpace_picks_its_router()
    {
        var site = Assert.Single(EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec.Sites);

        Assert.Equal(Caps.Isps[0].Name, site.Attach.Isp);
        Assert.Null(site.Attach.Router);
    }

    [Fact]
    public void A_domain_controller_gets_no_role_graySpace_cannot_configure_from_the_document()
    {
        var input = Office() with { Terrain = Office().Terrain with { Hosts = [new HostDto("dc-01", "Office LAN", "ubuntu", "domain controller")] } };

        var mapping = EnclaveSliceMapper.Map(input, Caps, Settings);

        Assert.DoesNotContain("ad-dc", Host(mapping.Spec, "dc-01").Roles ?? []);
        Assert.Contains(mapping.Notes, n => n.Contains("dc-01") && n.Contains("ad-dc"));
    }

    [Fact]
    public void The_ghosts_client_role_gets_the_client_and_api_urls_from_settings()
    {
        var vars = EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec.RoleVars!["ghosts-client"];

        Assert.Equal(("http://ghosts:5000/client/ghosts-client-universal.zip", "http://ghosts:5000/api", "ghosts"),
            (vars["client_url"], vars["api_url"], vars["api_host"]));
    }

    [Fact]
    public void Without_those_settings_the_spec_says_what_is_missing()
    {
        var mapping = EnclaveSliceMapper.Map(Office(), Caps, new TerrainProviderOptions());

        Assert.Contains(mapping.Notes, n => n.Contains("TerrainProvider:ClientUrl"));
        Assert.Contains(mapping.Notes, n => n.Contains("TerrainProvider:GhostsApiUrl"));
    }

    [Fact]
    public void A_draft_document_maps_the_same_as_the_scenario_rows()
    {
        var document = JsonNode.Parse("""
            {"schemaVersion":"1.2.0","slug":"office-day","name":"Office Day","description":"An ordinary day at the office.",
             "terrain":{"reference":{"provider":"grayspace","enclave":"office-enclave"},
                        "segments":[{"name":"Office LAN","cidr":"10.20.0.0/24"},{"name":"Front desk"}],
                        "hosts":[{"name":"desk-01","segment":"Office LAN","os":"ubuntu","role":"workstation"},
                                 {"name":"file-01","segment":"Office LAN","os":"ubuntu","role":"file server","services":["smb"]},
                                 {"name":"web-01","segment":"Front desk","os":"ubuntu","role":"workstation"},
                                 {"name":"console-01","segment":"Office LAN","os":"windows 11","role":"participant console"}],
                        "services":[{"name":"intranet web","hosts":["web-01"]}]},
             "population":{"pools":[{"role":"office staff","count":3,"segments":["Office LAN"]},{"role":"reception","count":1,"hosts":["desk-01"]}]}}
            """)!.AsObject();

        var fromDocument = EnclaveSliceMapper.Map(EnclaveSliceMapper.FromDocument(document), Caps, Settings).Spec;
        var fromRows = EnclaveSliceMapper.Map(Office(), Caps, Settings).Spec;

        Assert.Equal(JsonSerializer.Serialize(fromRows, GraySpaceClient.Wire), JsonSerializer.Serialize(fromDocument, GraySpaceClient.Wire));
    }

    private static SliceHost Host(SliceSpec spec, string name) => Assert.Single(spec.Hosts, h => h.Name == name);
}
