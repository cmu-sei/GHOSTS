// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Endpoints.GraySpace;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>A prefix other than the spec's runs a second copy; no version means the draft, saved first.</summary>
public record CreateInstance(string? Prefix, string? Version);

/// <summary>POST /scenarios/{id}/enclave/instance — asks GraySpace to build an instance of the enclave and records it and its job.</summary>
public class CreateInstanceEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/enclave/instance", async (int id, CreateInstance body, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (await EnclaveRules.Find(db, id, ct) is not { } enclave)
                    return ScenarioState.Missing(id);
                if (EnclaveRules.Slug(enclave) is not { } slug)
                    return EnclaveRules.Unnamed();

                var version = body.Version?.Trim();
                if (string.IsNullOrEmpty(version))
                {
                    await EnclaveRules.Save(grayspace, enclave, slug, ct);
                    EnclaveRules.Touch(db, enclave, http);
                    await db.SaveChangesAsync(ct);
                    version = enclave.Version;
                }
                var request = new JsonObject { ["slice"] = slug, ["version"] = version };
                if (!string.IsNullOrWhiteSpace(body.Prefix))
                    request["prefix"] = body.Prefix.Trim();

                var accepted = await grayspace.Json(HttpMethod.Post, "instances", request, ct);
                enclave.InstanceId = (string?)accepted?["instance"];
                enclave.JobId = EnclaveRules.JobOf(accepted);
                EnclaveRules.Touch(db, enclave, http);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new EnclaveAnswer(EnclaveView.From(enclave), accepted));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
