// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

/// <summary>GET /admin/libraries/{id} — a library, its draft templates, and its versions.</summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/libraries/{id:int}", async (int id, BuilderDb db, CancellationToken ct) =>
                await db.Libraries.FindAsync([id], ct) is { } library
                    ? Results.Ok(await LibraryViews.Detail(db, library, ct))
                    : LibraryViews.Missing(id))
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
