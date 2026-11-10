// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>DELETE /scenarios/{id}/enclave/instance — asks GraySpace to take the last instance down, and follows that job. GraySpace keeps the record.</summary>
public class DestroyInstanceEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/scenarios/{id:int}/enclave/instance", async (int id, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (enclave.InstanceId is not { } instanceId)
                    return ErrorResults.Conflict("NO_INSTANCE", "This scenario has no instance on GraySpace to destroy.");
                var accepted = await grayspace.Json(HttpMethod.Delete, $"instances/{Uri.EscapeDataString(instanceId)}", null, ct);
                enclave.JobId = EnclaveRules.JobOf(accepted);
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), accepted));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
