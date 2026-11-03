// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>GET /admin/scenario-types/{id} — a scenario type's draft and its published versions.</summary>
public class RetrieveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/scenario-types/{id}", async (string id, BuilderDb db, CancellationToken ct) =>
                await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is { } row
                    ? Results.Ok(await ScenarioTypeViews.Detail(db, row, ct))
                    : ScenarioTypeViews.Missing(id))
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
