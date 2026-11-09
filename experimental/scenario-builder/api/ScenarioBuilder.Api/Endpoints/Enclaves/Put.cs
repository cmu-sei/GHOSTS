// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

public record PutEnclave(JsonObject? Spec);

/// <summary>PUT /scenarios/{id}/enclave — saves the draft slice spec, as the page edits it. Nothing is sent to GraySpace; it checks the spec on validate and save.</summary>
public class PutEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/scenarios/{id:int}/enclave", async (int id, PutEnclave body, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (body.Spec is null)
                    return ErrorResults.BadRequest("SPEC_REQUIRED", "Send the enclave's slice spec as spec, a JSON object.");
                enclave.Spec = body.Spec.ToJsonString();
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                return Results.Ok(EnclaveView.From(enclave));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
