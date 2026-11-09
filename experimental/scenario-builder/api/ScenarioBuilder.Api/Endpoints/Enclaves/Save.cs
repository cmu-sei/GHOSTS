// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>POST /scenarios/{id}/enclave/save — stores the draft on GraySpace as a new version (the same one when nothing changed).</summary>
public class SaveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/enclave/save", async (int id, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (EnclaveRules.Slug(enclave) is not { } slug)
                    return EnclaveRules.Unnamed();
                var result = await EnclaveRules.Save(grayspace, enclave, slug, ct);
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), result));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
