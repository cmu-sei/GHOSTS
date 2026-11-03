// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>DELETE /admin/kinds/{id} — only a kind never published, which no scenario can be on. Others are retired instead.</summary>
public class DeleteEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapDelete("/admin/kinds/{id}", async (string id, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is not { } row)
                    return KindViews.Missing(id);
                if (await db.PublishedKinds.AnyAsync(p => p.KindId == id, ct))
                    return ErrorResults.Conflict("KIND_PUBLISHED", $"'{id}' has been published, so scenarios may be on it. Retire it instead.");
                db.Kinds.Remove(row);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
