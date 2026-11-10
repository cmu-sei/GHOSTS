// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

public record ReuseEnclave(string? Slug);

/// <summary>POST /scenarios/{id}/enclave/reuse — makes an enclave already on GraySpace this scenario's: its latest spec becomes the draft, at its version.</summary>
public class ReuseEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/enclave/reuse", async (int id, ReuseEnclave body, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (string.IsNullOrWhiteSpace(body.Slug))
                    return ErrorResults.BadRequest("SLUG_REQUIRED", "Name the enclave on GraySpace to reuse.");

                var detail = await grayspace.Get($"slices/{Uri.EscapeDataString(body.Slug.Trim())}", ct);
                if (detail?["spec"] is not JsonObject spec)
                    return ErrorResults.Conflict("ENCLAVE_HAS_NO_SPEC", $"{body.Slug} is GraySpace's base, not an enclave a scenario can reuse.");
                enclave.Spec = spec.ToJsonString();
                enclave.Version = (string?)detail["version"];
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), detail));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
