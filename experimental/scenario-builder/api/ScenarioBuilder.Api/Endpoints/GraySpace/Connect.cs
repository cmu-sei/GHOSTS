// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.GraySpace;

public record ConnectGraySpace(string? Url, string? Token);

/// <summary>
/// PUT /grayspace — an admin connects GraySpace, or moves it, by its address and its GRAYSPACE_API_TOKEN. They are saved
/// only once GraySpace answers GET /server with them, which it does with or without a deployed base.
/// </summary>
public class ConnectEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/grayspace", async (ConnectGraySpace body, HttpContext http, BuilderDb db, GraySpaceClient grayspace, CancellationToken ct) =>
            {
                if (!Uri.TryCreate(body.Url?.Trim(), UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
                    return ErrorResults.BadRequest("URL_INVALID", "Give GraySpace's address in full, starting with http:// or https://, such as http://grayspace.local:8000.");
                if (string.IsNullOrWhiteSpace(body.Token))
                    return ErrorResults.BadRequest("TOKEN_REQUIRED", "Give GraySpace's token: its GRAYSPACE_API_TOKEN.");

                var url = Address(body.Url!);
                var token = body.Token.Trim();
                await grayspace.Send(url, token, HttpMethod.Get, "server", null, ct);

                var connection = await db.GraySpaceConnections.FirstOrDefaultAsync(ct);
                if (connection is null)
                    db.GraySpaceConnections.Add(connection = new GraySpaceConnection());
                connection.Url = url;
                connection.Token = token;
                connection.ConnectedBy = Caller.From(http).User;
                connection.Connected = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(GraySpaceView.From(connection));
            })
            .AddEndpointFilter<AdminOnly>().AddEndpointFilter<GraySpaceFilter>().WithTags(tag);

    /// <summary>The address without a trailing slash or /api/v1, which every call adds.</summary>
    private static string Address(string url)
    {
        var address = url.Trim().TrimEnd('/');
        return address.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase) ? address[..^"/api/v1".Length] : address;
    }
}
