// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypes;

public record ScenarioTypeSummary(string Id, string Version, string Name);

/// <summary>GET /scenario-types — the scenario types a scenario can be created from: each one's latest version, retired ones left out.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenario-types", (ScenarioTypeCatalog scenarioTypes) => scenarioTypes.Offered.Select(k => new ScenarioTypeSummary(k.Id, k.Version, k.Name)))
            .WithTags(tag);
}
