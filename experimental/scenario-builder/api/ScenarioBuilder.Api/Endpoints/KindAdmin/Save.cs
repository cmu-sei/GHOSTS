// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>
/// PUT /admin/kinds/{id} — saves the draft: the kind's JSON, with what's left out filled in. Its id and version stay
/// as they are. A draft may be unfinished, but nothing is stored that breaks a kind rule.
/// </summary>
public class SaveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/admin/kinds/{id}", async (string id, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is not { } row)
                    return KindViews.Missing(id);
                var (body, error) = await KindViews.ReadBody(http, ct);
                if (error is not null)
                    return error;
                var draft = body!.Filled(id, KindViews.Draft(row).Version);
                if (KindCatalog.Problem(draft, finished: false) is { } problem)
                    return KindViews.Invalid(problem);
                row.Draft = KindStore.Write(draft);
                row.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(await KindViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
