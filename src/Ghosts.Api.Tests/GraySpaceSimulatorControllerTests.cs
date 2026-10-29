// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using System.Text.Json;
using Ghosts.Api.Controllers.Stubs;
using Ghosts.Api.Infrastructure.Terrain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ghosts.Api.Tests;

/// <summary>
/// The simulator's HTTP surface behaves as GraySpace's does: off unless enabled, 503 with no token configured, 401 for
/// a missing or wrong bearer token, snake_case JSON in and out, and FastAPI's {"detail": ...} for errors.
/// </summary>
public class GraySpaceSimulatorControllerTests
{
    private readonly GraySpaceSimulator _sim = new(() => DateTime.UtcNow, buildSeconds: 30);

    [Fact]
    public async Task With_the_simulator_off_every_call_is_not_found() =>
        Assert.Equal(404, Status(await Controller(enabled: false).GetBase(default)));

    [Fact]
    public async Task With_no_token_configured_calls_are_refused_as_graySpace_refuses_them()
    {
        var result = await Controller(configuredToken: "").GetBase(default);

        Assert.Equal(503, Status(result));
        Assert.Contains("GRAYSPACE_API_TOKEN", Detail(result));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer wrong")]
    public async Task A_missing_or_wrong_bearer_token_is_unauthorized(string? header) =>
        Assert.Equal(401, Status(await Controller(authorization: header).GetBase(default)));

    [Fact]
    public async Task Answers_are_snake_case()
    {
        var result = await Controller().GetCapabilities(default);

        Assert.Equal(200, Status(result));
        Assert.Contains("\"slice_features\":", Body(result));
        Assert.Empty(GraySpaceContract.Errors("Capabilities", Body(result)));
    }

    [Fact]
    public async Task A_slice_spec_is_read_from_snake_case_and_stored_under_its_path()
    {
        const string spec = """
            {"apiVersion":"grayspace/v1","kind":"Slice","slug":"office-day","prefix":"od","description":"An office",
             "sites":[{"name":"office","attach":{"isp":"Lumen"},"segments":[{"name":"office-lan"}]}],
             "hosts":[{"name":"staff","count":2,"segment":"office-lan","image":"ubuntu-22.04-lxc","roles":["ghosts-client"],"labels":{"ghosts.pool":"office staff"}}],
             "role_vars":{"ghosts-client":{"client_url":"http://g/c.zip","api_url":"http://g/api"}}}
            """;

        var result = await Controller(body: spec).PutSlice("office-day", default);

        Assert.Equal(200, Status(result));
        Assert.Empty(GraySpaceContract.Errors("PutSliceResult", Body(result)));
        Assert.Empty(JsonDocument.Parse(Body(result)).RootElement.GetProperty("findings").EnumerateArray());
    }

    [Fact]
    public async Task A_spec_whose_slug_differs_from_its_path_is_refused() =>
        Assert.Equal(422, Status(await Controller(body: """{"apiVersion":"grayspace/v1","kind":"Slice","slug":"other","prefix":"od"}""").PutSlice("office-day", default)));

    [Fact]
    public async Task A_body_that_is_not_json_is_a_validation_error()
    {
        var result = await Controller(body: "not json").CreateInstance(default);

        Assert.Equal(422, Status(result));
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(Body(result)).RootElement.GetProperty("detail").ValueKind);
    }

    [Fact]
    public async Task An_unknown_instance_is_not_found_with_graySpaces_error_body()
    {
        var result = await Controller().GetInstance("nope", default);

        Assert.Equal(404, Status(result));
        Assert.Equal("No instance 'nope'", Detail(result));
    }

    [Fact]
    public async Task An_accepted_build_is_202_with_its_job()
    {
        await _sim.PutSliceAsync(new SliceSpec("office-day", "od", "An office", "ghosts", [new SliceSite("office", new SliceAttach(Isp: "Lumen"), [new SliceSegment("lan")])], []), default);

        var result = await Controller(body: """{"slice":"office-day","prefix":"g7"}""").CreateInstance(default);

        Assert.Equal(202, Status(result));
        Assert.Empty(GraySpaceContract.Errors("JobAccepted", Body(result)));
    }

    [Fact]
    public async Task A_refused_create_answers_422_with_its_findings_as_graySpace_does()
    {
        await _sim.PutSliceAsync(new SliceSpec("office-day", "od", "An office", "ghosts",
            [new SliceSite("office", new SliceAttach(Isp: "Lumen"), [new SliceSegment("lan")])], [new SliceHost("pc", "windows-11", Segment: "lan")]), default);

        var result = await Controller(body: """{"slice":"office-day","prefix":"g7"}""").CreateInstance(default);

        Assert.Equal(422, Status(result));
        var detail = JsonDocument.Parse(Body(result)).RootElement.GetProperty("detail");
        Assert.Equal("UNKNOWN_IMAGE", detail.GetProperty("findings")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_job_log_streams_until_the_job_ends_unless_follow_is_false()
    {
        await _sim.PutSliceAsync(new SliceSpec("office-day", "od", "An office", "ghosts", [new SliceSite("office", new SliceAttach(Isp: "Lumen"), [new SliceSegment("lan")])], []), default);
        var job = (await _sim.CreateInstanceAsync(new CreateInstanceRequest("office-day", Prefix: "g7"), default)).Job.Id;
        using var soon = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        var now = await Controller().GetJobLog(job, follow: false, default);
        var following = Controller().GetJobLog(job, follow: true, soon.Token);
        await Task.WhenAny(following, Task.Delay(1000));

        Assert.Equal(200, Status(now));
        Assert.True(!following.IsCompletedSuccessfully || Status(following.Result) != 200, "a followed log returned before the job ended");
    }

    private GraySpaceSimulatorController Controller(bool enabled = true, string configuredToken = "dev-token", string? authorization = "Bearer dev-token", string? body = null)
    {
        var context = new DefaultHttpContext();
        if (authorization != null) context.Request.Headers.Authorization = authorization;
        if (body != null) context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return new GraySpaceSimulatorController(_sim, Options.Create(new TerrainProviderOptions { EnableStub = enabled, Token = configuredToken }))
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static int Status(IActionResult result) => Assert.IsType<ContentResult>(result).StatusCode ?? 200;

    private static string Body(IActionResult result) => Assert.IsType<ContentResult>(result).Content!;

    private static string Detail(IActionResult result) => JsonDocument.Parse(Body(result)).RootElement.GetProperty("detail").GetString()!;
}
