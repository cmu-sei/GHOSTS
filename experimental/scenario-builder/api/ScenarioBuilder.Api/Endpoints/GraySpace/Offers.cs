// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>What GraySpace offers an enclave, each part as GraySpace wrote it; <c>problems</c> says why a part is missing.</summary>
public record OffersView(JsonNode? Server, JsonNode? Capabilities, JsonNode? Base, JsonNode? Versions, JsonNode? AttachmentPoints, JsonNode? Map, JsonNode? Slices, Dictionary<string, string> Problems);

/// <summary>
/// GET /grayspace/offers — the connected GraySpace's server (images, roles, jobs, capacity), the deployed base's
/// capabilities (DNS, CAs, ISPs), base, the base versions it ships, attachment points, map and enclaves. The server must
/// answer; any other part GraySpace can't give, such as capabilities with no base deployed, is left out, with its reason.
/// </summary>
public class OffersEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/grayspace/offers", async (GraySpaceClient grayspace, CancellationToken ct) =>
            {
                var server = await grayspace.Get("server", ct);
                var problems = new Dictionary<string, string>();

                async Task<JsonNode?> Part(string name, string path)
                {
                    try
                    {
                        return await grayspace.Get(path, ct);
                    }
                    catch (GraySpaceException e)
                    {
                        problems[name] = e.Message;
                        return null;
                    }
                }

                return Results.Ok(new OffersView(server, await Part("capabilities", "capabilities"), await Part("base", "base"),
                    await Part("versions", "base/versions"), await Part("attachmentPoints", "attachment-points"), await Part("map", "map"),
                    await Part("slices", "slices"), problems));
            })
            .AddEndpointFilter<GraySpaceFilter>().WithTags(tag);
}
