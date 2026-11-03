// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

/// <summary>GET /admin/libraries — every template library, by name, with its latest version.</summary>
public class ListEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/admin/libraries", async (BuilderDb db, CancellationToken ct) =>
            {
                var versions = (await db.LibraryVersions.ToListAsync(ct)).ToLookup(v => v.LibraryId);
                return (await db.Libraries.ToListAsync(ct)).OrderBy(l => l.Name)
                    .Select(l => LibraryViews.Summary(l, versions[l.Id].ToList()));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
