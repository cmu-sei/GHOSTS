// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>GET /admin/kinds — every kind, retired ones too, with its latest version and how many scenarios use it.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/kinds", (BuilderDb db, CancellationToken ct) => KindViews.List(db, ct))
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
