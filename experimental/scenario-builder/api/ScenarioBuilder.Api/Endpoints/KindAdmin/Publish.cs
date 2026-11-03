// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>
/// POST /admin/kinds/{id}/publish — the draft, finished, becomes the next version, which new scenarios get.
/// Scenarios already made stay on the version they were made on.
/// </summary>
public class PublishEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/kinds/{id}/publish", async (string id, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Kinds.FirstOrDefaultAsync(k => k.KindId == id, ct) is not { } row)
                    return KindViews.Missing(id);
                var draft = KindViews.Draft(row);
                if (KindCatalog.Problem(draft) is { } problem)
                    return KindViews.Invalid($"It can't be published yet: {problem}");
                var latest = await db.PublishedKinds.Where(p => p.KindId == id).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
                if (latest is not null && KindStore.SameAs(draft, KindStore.Read(latest.Content)))
                    return ErrorResults.Conflict("NOTHING_TO_PUBLISH", $"Nothing has changed since version {latest.Version}.");

                var published = await KindStore.Publish(db, kinds, draft, KindStore.Next(latest?.Version), Caller.From(http).User, ct);
                return Results.Ok(new KindVersionView(published.Version, published.PublishedBy, published.Published, 0));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
