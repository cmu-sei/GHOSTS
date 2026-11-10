// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>POST /scenarios/{id}/enclave/job/{cancel|retry} — cancels the last job, or retries it from the stage it failed at and follows the new one.</summary>
public class JobEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/enclave/job/{action}", async (int id, string action, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (action is not ("cancel" or "retry"))
                    return ErrorResults.NotFound("ACTION_UNKNOWN", $"A job can be cancelled or retried, not '{action}'.");
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (enclave.JobId is not { } jobId)
                    return ErrorResults.Conflict("NO_JOB", "This scenario hasn't started a job on GraySpace.");

                var result = await grayspace.Json(HttpMethod.Post, $"jobs/{Uri.EscapeDataString(jobId)}/{action}", null, ct);
                if (action == "retry")
                    enclave.JobId = EnclaveRules.JobOf(result);
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), result));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
