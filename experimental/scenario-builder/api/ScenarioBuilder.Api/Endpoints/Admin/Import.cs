// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

/// <summary>
/// POST /admin/libraries/import — a <c>.library.json</c> file as the body. A new slug becomes a new library at
/// version 1; a known slug gets its next version. Either way the draft is reset to the file.
/// </summary>
public class ImportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/libraries/import", async (HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                LibraryFile? file;
                try
                {
                    file = await JsonSerializer.DeserializeAsync<LibraryFile>(http.Request.Body, LibraryFile.Json, ct);
                }
                catch (JsonException e)
                {
                    return ErrorResults.BadRequest("LIBRARY_INVALID", $"That isn't a library file: {e.Message}");
                }
                if (file is null)
                    return ErrorResults.BadRequest("LIBRARY_INVALID", "That isn't a library file: it's empty.");
                if (LibraryFile.Check(file, kinds.All.Select(k => k.Id).ToList()) is { } problem)
                    return ErrorResults.BadRequest("LIBRARY_INVALID", problem);

                var (library, _) = await LibraryImport.Apply(db, file, Caller.From(http).User, ct);
                return Results.Ok(await LibraryViews.Detail(db, library, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
