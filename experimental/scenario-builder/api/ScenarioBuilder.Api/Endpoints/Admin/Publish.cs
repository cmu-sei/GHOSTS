// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

/// <summary>POST /admin/libraries/{id}/publish — the draft becomes the next numbered version.</summary>
public class PublishEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/libraries/{id:int}/publish", async (int id, HttpContext http, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Libraries.FindAsync([id], ct) is not { } library)
                    return LibraryViews.Missing(id);

                var draft = LibraryFile.Of(library, 0);
                var latest = await db.LibraryVersions.Where(v => v.LibraryId == id).OrderByDescending(v => v.Number).FirstOrDefaultAsync(ct);
                if (latest is null && draft.Templates.Count == 0)
                    return ErrorResults.Conflict("NOTHING_TO_PUBLISH", "Add a template before publishing.");
                if (latest is not null && draft.SameAs(LibraryFile.Read(latest.Content)))
                    return ErrorResults.Conflict("NOTHING_TO_PUBLISH", $"Nothing has changed since version {latest.Number}.");

                var (_, version) = await LibraryImport.Apply(db, draft, Caller.From(http).User, ct);
                return Results.Ok(VersionView.From(version));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
