// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints.Kinds;

/// <summary>
/// GET /kinds/{id}?version= — one published kind: stages, questions, gates and drafts. Its latest version, or the
/// version given, which is how a scenario gets the kind it was made on.
/// </summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/kinds/{id}", (string id, string? version, KindCatalog kinds) =>
                (version is null ? kinds.Find(id) : kinds.Find(id, version)) is { } kind
                    ? Results.Ok(kind)
                    : ErrorResults.NotFound("KIND_UNKNOWN",
                        $"There is no scenario type '{id}'{(version is null ? "" : $" at version {version}")}. Scenario types: {string.Join(", ", kinds.All.Select(k => k.Id))}."))
            .WithTags(tag);
}
