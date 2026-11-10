// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>GET /scenarios/{id}/enclave — the scenario's enclave; a new one, named from the scenario, until it's first changed.</summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/enclave", async (int id, BuilderDb db, CancellationToken ct) =>
                await EnclaveRules.Find(db, id, ct) is { } enclave ? Results.Ok(EnclaveView.From(enclave)) : ScenarioState.Missing(id))
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
