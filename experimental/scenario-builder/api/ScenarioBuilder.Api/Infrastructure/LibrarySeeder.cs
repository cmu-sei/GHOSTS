// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Infrastructure;

/// <summary>
/// An installation with no libraries starts with every <c>.library.json</c> in the directory, each as version 1
/// by <c>system</c>. Once any library exists it does nothing, so an admin's changes are never overwritten.
/// </summary>
public static class LibrarySeeder
{
    public const string By = "system";

    public static async Task Seed(BuilderDb db, KindCatalog kinds, string directory, CancellationToken ct)
    {
        if (await db.Libraries.AnyAsync(ct))
            return;
        var known = kinds.All.Select(k => k.Id).ToList();
        foreach (var path in Directory.GetFiles(directory, "*.library.json").Order())
        {
            var file = LibraryFile.Read(await File.ReadAllTextAsync(path, ct));
            if (LibraryFile.Check(file, known) is { } problem)
                throw new InvalidOperationException($"{Path.GetFileName(path)}: {problem}");
            await LibraryImport.Apply(db, file, By, ct);
        }
    }
}
