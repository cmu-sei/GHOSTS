// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ScenarioBuilder.Api.Domain;

/// <summary>
/// One version of one library, as <c>.library.json</c>: what export writes, import reads, and each published
/// version stores. It carries everything, so a library can be carried onto a closed range.
/// </summary>
public partial record LibraryFile(
    string Format,
    int FormatVersion,
    string Slug,
    string Name,
    string? Description,
    List<string> Kinds,
    int Version,
    List<TemplateSpec> Templates)
{
    public const string FormatName = "scenario-builder-library";
    public const int CurrentFormatVersion = 1;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true
    };

    /// <summary>A library's draft, as the file it would publish as <paramref name="version"/>.</summary>
    public static LibraryFile Of(TemplateLibrary library, int version) => new(
        FormatName, CurrentFormatVersion, library.Slug, library.Name, library.Description,
        JsonSerializer.Deserialize<List<string>>(library.Kinds, Json)!, version,
        JsonSerializer.Deserialize<List<TemplateSpec>>(library.Draft, Json)!);

    public static LibraryFile Read(string content) => JsonSerializer.Deserialize<LibraryFile>(content, Json)!;

    public string Write() => JsonSerializer.Serialize(this, Json);

    /// <summary>Same content, whatever the version numbers.</summary>
    public bool SameAs(LibraryFile other) =>
        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(this with { Version = 0 }, Json), JsonSerializer.SerializeToNode(other with { Version = 0 }, Json));

    /// <summary>Null when the file is a library this installation can use; otherwise the first thing wrong with it.</summary>
    public static string? Check(LibraryFile f, IReadOnlyCollection<string> knownKinds)
    {
        if (f.Format != FormatName)
            return $"The file's \"format\" is \"{f.Format}\", not \"{FormatName}\", so it isn't a library file.";
        if (f.FormatVersion != CurrentFormatVersion)
            return $"The file's formatVersion is {f.FormatVersion}; this builder reads version {CurrentFormatVersion}.";
        if (f.Slug is null || !Kebab().IsMatch(f.Slug))
            return $"The slug \"{f.Slug}\" must be lower-case words joined by hyphens.";
        if (string.IsNullOrWhiteSpace(f.Name))
            return "Give the library a name.";
        if (f.Kinds is null || f.Templates is null)
            return "A library file lists its kinds and its templates.";
        foreach (var kind in f.Kinds)
            if (kind is null || !knownKinds.Contains(kind))
                return $"There is no kind \"{kind}\" here. Kinds: {string.Join(", ", knownKinds)}.";

        var ids = new HashSet<string>();
        foreach (var t in f.Templates)
        {
            if (t is null)
                return "One of the templates is empty.";
            if (t.Id is null || !Kebab().IsMatch(t.Id))
                return $"The template id \"{t.Id}\" must be lower-case words joined by hyphens.";
            if (!ids.Add(t.Id))
                return $"The template id \"{t.Id}\" is used twice.";
            if (string.IsNullOrWhiteSpace(t.Name))
                return $"Give the template \"{t.Id}\" a name.";
            // The enum converter also reads numbers; only the names mean anything.
            if (!Enum.IsDefined(t.Audience))
                return $"The template \"{t.Name}\" has the audience {(int)t.Audience}; use participants, facilitators or designers.";
            if (!Enum.IsDefined(t.Format))
                return $"The template \"{t.Name}\" has the format {(int)t.Format}; use html or markdown.";
            if (t.Body is null)
                return $"The template \"{t.Name}\" has no body.";
            if (TemplateRenderer.Check(t) is { } error)
                return error;
        }
        return null;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Kebab();
}
