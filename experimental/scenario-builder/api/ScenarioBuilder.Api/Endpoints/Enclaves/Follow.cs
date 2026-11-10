// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>The enclave, its last instance and last job as GraySpace reports them, and the job's log so far.</summary>
public record FollowView(EnclaveView Enclave, JsonNode? Instance, JsonNode? Job, string? Log);

/// <summary>GET /scenarios/{id}/enclave/instance — what GraySpace says of the scenario's last instance and job; the page polls it while a job runs.</summary>
public class FollowEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/enclave/instance", async (int id, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                var instance = enclave.InstanceId is { } instanceId ? await grayspace.Get($"instances/{Uri.EscapeDataString(instanceId)}", ct) : null;
                JsonNode? job = null;
                string? log = null;
                if (enclave.JobId is { } jobId)
                {
                    job = await grayspace.Get($"jobs/{Uri.EscapeDataString(jobId)}", ct);
                    log = await grayspace.Text(HttpMethod.Get, $"jobs/{Uri.EscapeDataString(jobId)}/log?follow=false", null, ct);
                }
                return Results.Ok(new FollowView(EnclaveView.From(enclave), instance, job, log));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
