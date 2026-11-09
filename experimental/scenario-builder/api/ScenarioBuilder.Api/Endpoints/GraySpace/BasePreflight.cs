// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>
/// GET /grayspace/base/preflight?version=&amp;scope= — an admin asks whether GraySpace's site can deploy a base version at
/// a scope: each check as GraySpace reports it. A deploy is refused while any is an error.
/// </summary>
public class BasePreflightEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/grayspace/base/preflight", async (string? version, string? scope, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                var query = new List<string>();
                if (!string.IsNullOrWhiteSpace(version))
                    query.Add($"version={Uri.EscapeDataString(version.Trim())}");
                if (!string.IsNullOrWhiteSpace(scope))
                    query.Add($"scope={Uri.EscapeDataString(scope.Trim())}");
                return Results.Ok(await grayspace.Get(query.Count == 0 ? "base/preflight" : $"base/preflight?{string.Join('&', query)}", ct));
            })
            .AddEndpointFilter<AdminOnly>().AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
