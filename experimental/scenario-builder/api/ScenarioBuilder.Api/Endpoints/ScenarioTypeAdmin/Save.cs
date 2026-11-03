// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

/// <summary>
/// PUT /admin/scenario-types/{id} — saves the draft: the scenario type's JSON, with what's left out filled in. Its id and version stay
/// as they are. A draft may be unfinished, but nothing is stored that breaks a scenario type rule.
/// </summary>
public class SaveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/admin/scenario-types/{id}", async (string id, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is not { } row)
                    return ScenarioTypeViews.Missing(id);
                var (body, error) = await ScenarioTypeViews.ReadBody(http, ct);
                if (error is not null)
                    return error;
                var draft = body!.Filled(id, ScenarioTypeViews.Draft(row).Version);
                if (ScenarioTypeCatalog.Problem(draft, finished: false) is { } problem)
                    return ScenarioTypeViews.Invalid(problem);
                row.Draft = ScenarioTypeStore.Write(draft);
                row.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(await ScenarioTypeViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
