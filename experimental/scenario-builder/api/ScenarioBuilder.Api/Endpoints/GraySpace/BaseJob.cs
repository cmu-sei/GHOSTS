// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>The base's latest job as GraySpace reports it, and its log so far; neither before the first.</summary>
public record BaseJobView(JsonNode? Job, string? Log);

/// <summary>GET /grayspace/base/job — GraySpace's latest base job, whoever started it; the page polls it while the job runs.</summary>
public class BaseJobEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/grayspace/base/job", async (GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await Latest(grayspace, ct) is not { } job)
                    return Results.Ok(new BaseJobView(null, null));
                var log = await grayspace.Text(HttpMethod.Get, $"jobs/{Uri.EscapeDataString((string)job["id"]!)}/log?follow=false", null, ct);
                return Results.Ok(new BaseJobView(job, log));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);

    /// <summary>The newest job GraySpace has run on the base, or null.</summary>
    public static async Task<JsonNode?> Latest(GraySpaceClient grayspace, CancellationToken ct) =>
        await grayspace.Get("jobs?target=base&limit=1", ct) is JsonArray { Count: > 0 } jobs ? jobs[0] : null;
}
