// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>GET /admin/kinds/{id} — a kind's draft and its published versions.</summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/kinds/{id}", async (string id, BuilderDb db, CancellationToken ct) =>
                await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is { } row
                    ? Results.Ok(await KindViews.Detail(db, row, ct))
                    : KindViews.Missing(id))
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
