// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

/// <summary>GET /admin/libraries/{id}/export — the latest published version as a <c>.library.json</c> file.</summary>
public class ExportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/libraries/{id:int}/export", async (int id, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Libraries.FindAsync([id], ct) is not { } library)
                    return LibraryViews.Missing(id);
                var latest = await db.LibraryVersions.Where(v => v.LibraryId == id).OrderByDescending(v => v.Number).FirstOrDefaultAsync(ct);
                return latest is null
                    ? ErrorResults.Conflict("NOT_PUBLISHED", $"{library.Name} has no published version yet. Publish it first.")
                    : Results.File(Encoding.UTF8.GetBytes(latest.Content), "application/json", $"{library.Slug}.library.json");
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
