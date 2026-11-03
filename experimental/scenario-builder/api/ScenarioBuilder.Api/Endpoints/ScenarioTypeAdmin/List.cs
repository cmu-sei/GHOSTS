// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>GET /admin/scenario-types — every scenario type, retired ones too, with its latest version and how many scenarios use it.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/scenario-types", (BuilderDb db, CancellationToken ct) => ScenarioTypeViews.List(db, ct))
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
