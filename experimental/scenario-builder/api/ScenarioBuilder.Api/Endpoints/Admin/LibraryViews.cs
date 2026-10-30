// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

public record LibrarySummary(int Id, string Slug, string Name, List<string> Kinds, int? LatestVersion, bool DraftChanged);

public record VersionView(int Number, string PublishedBy, DateTime Published)
{
    public static VersionView From(LibraryVersion v) => new(v.Number, v.PublishedBy, v.Published);
}

public record LibraryDetail(
    int Id, string Slug, string Name, string? Description, List<string> Kinds, List<TemplateSpec> Templates,
    int? LatestVersion, bool DraftChanged, List<VersionView> Versions);

public static class LibraryViews
{
    public static IResult Missing(int id) => ErrorResults.NotFound("LIBRARY_UNKNOWN", $"There is no library {id}.");

    public static LibrarySummary Summary(TemplateLibrary library, IReadOnlyList<LibraryVersion> versions)
    {
        var draft = LibraryFile.Of(library, 0);
        var latest = versions.MaxBy(v => v.Number);
        return new LibrarySummary(library.Id, library.Slug, library.Name, draft.Kinds, latest?.Number, DraftChanged(draft, latest));
    }

    public static async Task<LibraryDetail> Detail(BuilderDb db, TemplateLibrary library, CancellationToken ct)
    {
        var versions = await db.LibraryVersions.Where(v => v.LibraryId == library.Id).OrderBy(v => v.Number).ToListAsync(ct);
        var draft = LibraryFile.Of(library, 0);
        var latest = versions.LastOrDefault();
        return new LibraryDetail(library.Id, library.Slug, library.Name, library.Description, draft.Kinds, draft.Templates,
            latest?.Number, DraftChanged(draft, latest), versions.Select(VersionView.From).ToList());
    }

    /// <summary>Before the first publish, any template is a change.</summary>
    private static bool DraftChanged(LibraryFile draft, LibraryVersion? latest) =>
        latest is null ? draft.Templates.Count > 0 : !draft.SameAs(LibraryFile.Read(latest.Content));
}
