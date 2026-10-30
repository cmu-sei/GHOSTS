// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints.Kinds;

public record KindSummary(string Id, string Version, string Name);

/// <summary>GET /kinds — the kinds a scenario can be created from.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/kinds", (KindCatalog kinds) => kinds.All.Select(k => new KindSummary(k.Id, k.Version, k.Name)))
            .WithTags(tag);
}
