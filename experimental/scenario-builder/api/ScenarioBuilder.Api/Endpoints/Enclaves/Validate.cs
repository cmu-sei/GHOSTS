// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>POST /scenarios/{id}/enclave/validate — GraySpace checks the draft without storing it: valid, and its findings.</summary>
public class ValidateEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/enclave/validate", async (int id, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (EnclaveRules.Slug(enclave) is not { } slug)
                    return EnclaveRules.Unnamed();
                var result = await grayspace.Json(HttpMethod.Post, $"slices/{Uri.EscapeDataString(slug)}/validate", EnclaveRules.Spec(enclave), ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), result));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
