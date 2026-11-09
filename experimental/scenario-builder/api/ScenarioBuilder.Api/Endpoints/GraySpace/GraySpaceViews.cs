// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

/// <summary>Where GraySpace is connected. The token is never in it.</summary>
public record GraySpaceView(bool Connected, string? Url, string? ConnectedBy, DateTime? Since)
{
    public static GraySpaceView From(GraySpaceConnection? c) => new(c is not null, c?.Url, c?.ConnectedBy, c?.Connected);
}

/// <summary>A GraySpace call that didn't succeed: the builder's code and message, and GraySpace's own detail (its findings, if any).</summary>
public record GraySpaceError(string Code, string Message, JsonNode? Detail);

/// <summary>On every GraySpace and enclave route: only a person deploys, and a failed GraySpace call answers as a <see cref="GraySpaceError"/>.</summary>
public sealed class GraySpaceFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (Caller.From(context.HttpContext).IsAgent)
            return ErrorResults.Forbidden("AGENT_CANNOT_DEPLOY", "Only a person works with GraySpace, on the Topology page.");
        try
        {
            return await next(context);
        }
        catch (GraySpaceException e)
        {
            return Results.Json(new GraySpaceError(e.Code, e.Message, e.Detail), statusCode: e.Status);
        }
    }
}
