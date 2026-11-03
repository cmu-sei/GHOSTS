// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.RegularExpressions;

namespace ScenarioBuilder.Api.Domain;

public static partial class Slug
{
    /// <summary>The name in lower-case kebab form, numbered -2, -3… when that slug is taken.</summary>
    public static string Unique(string name, ISet<string> taken)
    {
        var stem = NotSlug().Replace(name.ToLowerInvariant(), "-").Trim('-');
        if (stem.Length == 0)
            stem = "scenario";
        var slug = stem;
        for (var n = 2; taken.Contains(slug); n++)
            slug = $"{stem}-{n}";
        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotSlug();
}
