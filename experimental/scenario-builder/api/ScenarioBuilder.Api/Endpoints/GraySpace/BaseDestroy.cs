// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>
/// DELETE /grayspace/base — an admin destroys the base, as a job. Never with destroy_instances: while instances are live,
/// GraySpace refuses and names them, and each is taken down on its own first.
/// </summary>
public class BaseDestroyEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/grayspace/base", async (GraySpaceClient grayspace, CancellationToken ct) =>
                Results.Ok(await grayspace.Json(HttpMethod.Delete, "base", null, ct)))
            .AddEndpointFilter<AdminOnly>().AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
