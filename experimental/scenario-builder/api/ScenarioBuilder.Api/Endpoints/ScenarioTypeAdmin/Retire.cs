// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.ScenarioTypeAdmin;

public record RetireScenarioType(bool Retired);

/// <summary>PUT /admin/scenario-types/{id}/retired — a retired scenario type isn't offered for new scenarios; scenarios on it carry on.</summary>
public class RetireEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/admin/scenario-types/{id}/retired", async (string id, RetireScenarioType body, ScenarioTypeCatalog scenarioTypes, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.ScenarioTypes.FirstOrDefaultAsync(k => k.ScenarioTypeId == id, ct) is not { } row)
                    return ScenarioTypeViews.Missing(id);
                row.Retired = body.Retired;
                row.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                scenarioTypes.SetRetired(id, body.Retired);
                return Results.Ok(await ScenarioTypeViews.Detail(db, row, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
