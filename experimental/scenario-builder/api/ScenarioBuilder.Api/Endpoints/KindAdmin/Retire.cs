// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

public record RetireKind(bool Retired);

/// <summary>PUT /admin/kinds/{id}/retired — a retired kind isn't offered for new scenarios; scenarios on it carry on.</summary>
public class RetireEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/admin/kinds/{id}/retired", async (string id, RetireKind body, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is not { } row)
                    return KindViews.Missing(id);
                row.Retired = body.Retired;
                row.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                kinds.SetRetired(id, body.Retired);
                return Results.Ok(await KindViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
