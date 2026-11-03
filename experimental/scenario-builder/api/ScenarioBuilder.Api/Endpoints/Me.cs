// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

namespace ScenarioBuilder.Api.Endpoints;

public record MeView(string User, bool Admin);

/// <summary>GET /me — who is calling and whether they're an admin, so the UI knows whether to offer Admin.</summary>
public class MeEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/me", (HttpContext http, IConfiguration config) =>
            {
                var caller = Caller.From(http);
                return new MeView(caller.User, Caller.IsAdmin(caller, config));
            })
            .WithTags("Me");
}
