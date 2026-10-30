// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

public record SaveLibrary(string? Name, string? Description, List<string>? Kinds, List<TemplateSpec>? Templates);

/// <summary>
/// PUT /admin/libraries/{id} — saves the draft. Nothing is stored unless every template parses, so a broken
/// template never reaches a published version.
/// </summary>
public class SaveEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPut("/admin/libraries/{id:int}", async (int id, SaveLibrary body, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                if (await db.Libraries.FindAsync([id], ct) is not { } library)
                    return LibraryViews.Missing(id);

                var file = new LibraryFile(LibraryFile.FormatName, LibraryFile.CurrentFormatVersion, library.Slug, body.Name?.Trim() ?? "",
                    body.Description, body.Kinds ?? [], 0, body.Templates ?? []);
                foreach (var t in file.Templates)
                    if (t?.Body is not null && TemplateRenderer.Check(t) is { } error)
                        return ErrorResults.BadRequest("TEMPLATE_INVALID", error);
                if (LibraryFile.Check(file, kinds.All.Select(k => k.Id).ToList()) is { } problem)
                    return ErrorResults.BadRequest("LIBRARY_INVALID", problem);

                library.Name = file.Name;
                library.Description = file.Description;
                library.Kinds = JsonSerializer.Serialize(file.Kinds, LibraryFile.Json);
                library.Draft = JsonSerializer.Serialize(file.Templates, LibraryFile.Json);
                library.Updated = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return Results.Ok(await LibraryViews.Detail(db, library, ct));
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
