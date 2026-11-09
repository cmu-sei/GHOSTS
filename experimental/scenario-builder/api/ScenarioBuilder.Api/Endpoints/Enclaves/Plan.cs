// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>POST /scenarios/{id}/enclave/plan — saves the draft, then starts GraySpace's plan job for that version and follows it.</summary>
public class PlanEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/enclave/plan", async (int id, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (EnclaveRules.Slug(enclave) is not { } slug)
                    return EnclaveRules.Unnamed();
                await EnclaveRules.Save(grayspace, enclave, slug, ct);
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                var accepted = await grayspace.Json(HttpMethod.Post, $"slices/{Uri.EscapeDataString(slug)}/plan", new JsonObject { ["version"] = enclave.Version }, ct);
                enclave.JobId = EnclaveRules.JobOf(accepted);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), accepted));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
