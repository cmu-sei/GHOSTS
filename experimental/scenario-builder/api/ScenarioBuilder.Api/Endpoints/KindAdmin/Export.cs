// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.KindAdmin;

/// <summary>GET /admin/kinds/{id}/export — the latest published version as a <c>.kind.json</c> file.</summary>
public class ExportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/kinds/{id}/export", async (string id, BuilderDb db, CancellationToken ct) =>
            {
                if (!await db.Kinds.AnyAsync(k => k.KindId == id, ct))
                    return KindViews.Missing(id);
                var latest = await db.PublishedKinds.Where(p => p.KindId == id).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
                return latest is null
                    ? ErrorResults.Conflict("NOT_PUBLISHED", $"'{id}' has no published version yet. Publish it first.")
                    : Results.File(Encoding.UTF8.GetBytes(latest.Content), "application/json", $"{id}.kind.json");
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
