// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>GET /admin/scenario-types/{id}/export — the latest published version as a <c>.scenario-type.json</c> file.</summary>
public class ExportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/scenario-types/{id}/export", async (string id, BuilderDb db, CancellationToken ct) =>
            {
                if (!await db.ScenarioTypes.AnyAsync(k => k.ScenarioTypeId == id, ct))
                    return ScenarioTypeViews.Missing(id);
                var latest = await db.PublishedScenarioTypes.Where(p => p.ScenarioTypeId == id).OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
                return latest is null
                    ? ErrorResults.Conflict("NOT_PUBLISHED", $"'{id}' has no published version yet. Publish it first.")
                    : Results.File(Encoding.UTF8.GetBytes(latest.Content), "application/json", $"{id}.scenario-type.json");
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
