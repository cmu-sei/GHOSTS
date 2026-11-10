// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>POST /grayspace/base/job/{cancel|retry} — an admin cancels the base's latest job, or retries it from the stage it failed at.</summary>
public class BaseJobActionEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/grayspace/base/job/{action}", async (string action, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (action is not ("cancel" or "retry"))
                    return ErrorResults.NotFound("ACTION_UNKNOWN", $"A job can be cancelled or retried, not '{action}'.");
                if (await BaseJobEndpoint.Latest(grayspace, ct) is not { } job)
                    return ErrorResults.Conflict("NO_JOB", "GraySpace hasn't run a job on the base yet.");
                return Results.Ok(await grayspace.Json(HttpMethod.Post, $"jobs/{Uri.EscapeDataString((string)job["id"]!)}/{action}", null, ct));
            })
            .AddEndpointFilter<AdminOnly>().AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
