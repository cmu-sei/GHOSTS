// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints.Kinds;

/// <summary>GET /kinds/{id} — one kind as loaded: stages, questions, gates and drafts.</summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/kinds/{id}", (string id, KindCatalog kinds) =>
                kinds.Find(id) is { } kind
                    ? Results.Ok(kind)
                    : ErrorResults.NotFound("KIND_UNKNOWN", $"There is no kind '{id}'. Kinds: {string.Join(", ", kinds.All.Select(k => k.Id))}."))
            .WithTags(tag);
}
