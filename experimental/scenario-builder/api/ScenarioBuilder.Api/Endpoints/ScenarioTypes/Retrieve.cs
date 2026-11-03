// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypes;

/// <summary>
/// GET /scenario-types/{id}?version= — one published scenario type: stages, questions, gates and drafts. Its latest version, or the
/// version given, which is how a scenario gets the scenario type it was made on.
/// </summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenario-types/{id}", (string id, string? version, ScenarioTypeCatalog scenarioTypes) =>
                (version is null ? scenarioTypes.Find(id) : scenarioTypes.Find(id, version)) is { } scenarioType
                    ? Results.Ok(scenarioType)
                    : ErrorResults.NotFound("SCENARIO_TYPE_UNKNOWN",
                        $"There is no scenario type '{id}'{(version is null ? "" : $" at version {version}")}. Scenario types: {string.Join(", ", scenarioTypes.All.Select(k => k.Id))}."))
            .WithTags(tag);
}
