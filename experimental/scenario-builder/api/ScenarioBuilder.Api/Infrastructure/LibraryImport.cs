// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>
/// The one way a version is made. A checked file becomes the next version of the library with its slug, or
/// version 1 of a new library, and the draft is reset to it. Import, publish and the starter library all use it.
/// </summary>
public static class LibraryImport
{
    public static async Task<(TemplateLibrary Library, LibraryVersion Version)> Apply(BuilderDb db, LibraryFile file, string by, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var library = await db.Libraries.FirstOrDefaultAsync(l => l.Slug == file.Slug, ct);
        if (library is null)
        {
            library = new TemplateLibrary { Slug = file.Slug, Created = now };
            db.Libraries.Add(library);
        }
        library.Name = file.Name;
        library.Description = file.Description;
        library.Kinds = JsonSerializer.Serialize(file.Kinds, LibraryFile.Json);
        library.Draft = JsonSerializer.Serialize(file.Templates, LibraryFile.Json);
        library.Updated = now;
        await db.SaveChangesAsync(ct);

        var number = (await db.LibraryVersions.Where(v => v.LibraryId == library.Id).MaxAsync(v => (int?)v.Number, ct) ?? 0) + 1;
        var version = new LibraryVersion
        {
            LibraryId = library.Id, Number = number, Content = (file with { Version = number }).Write(), PublishedBy = by, Published = now
        };
        db.LibraryVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return (library, version);
    }
}
