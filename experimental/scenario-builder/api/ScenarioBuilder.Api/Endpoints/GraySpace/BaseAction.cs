// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>The base version and scope to deploy; GraySpace keeps the deployed ones for any left out.</summary>
public record BaseActionRequest(string? Version, string? Scope);

/// <summary>
/// POST /grayspace/base/{deploy|configure} — an admin deploys a base version at a scope, or configures the deployed base
/// again. GraySpace runs either as a job, which GET /grayspace/base/job follows.
/// </summary>
public class BaseActionEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/grayspace/base/{action}", async (string action, BaseActionRequest? body, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (action is not ("deploy" or "configure"))
                    return ErrorResults.NotFound("ACTION_UNKNOWN", $"The base can be deployed or configured, not '{action}'.");
                var request = action == "deploy" ? new JsonObject { ["version"] = body?.Version, ["scope"] = body?.Scope } : new JsonObject();
                return Results.Ok(await grayspace.Json(HttpMethod.Post, $"base/{action}", request, ct));
            })
            .AddEndpointFilter<AdminOnly>().AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
