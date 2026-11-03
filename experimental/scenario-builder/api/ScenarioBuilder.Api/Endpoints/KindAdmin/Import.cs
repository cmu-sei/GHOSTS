// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>
/// POST /admin/kinds/import — a <c>.kind.json</c> file as the body, published as it comes. A new id becomes a new kind
/// at the file's version; a known id gets its next version. Either way the draft is reset to the file.
/// </summary>
public class ImportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/kinds/import", async (HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                var (kind, error) = await KindViews.ReadBody(http, ct);
                if (error is not null)
                    return error;
                if (KindCatalog.Problem(kind!) is { } problem)
                    return KindViews.Invalid(problem);
                var latest = await db.PublishedKinds.Where(p => p.KindId == kind!.Id).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
                var version = latest is not null ? KindStore.Next(latest.Version) : string.IsNullOrWhiteSpace(kind!.Version) ? KindStore.Next(null) : kind.Version;
                await KindStore.Publish(db, kinds, kind!, version, Caller.From(http).User, ct);
                var row = await db.Kinds.FirstAsync(k => k.KindId == kind!.Id, ct);
                return Results.Ok(await KindViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
