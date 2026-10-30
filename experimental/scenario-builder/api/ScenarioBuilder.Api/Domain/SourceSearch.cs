// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.RegularExpressions;

namespace ScenarioBuilder.Api.Domain;

public record Passage(Source Source, int Number, int Of, string Text);

/// <summary>
/// Finds the passages of a scenario's sources that a question is about, by the words they share. No embeddings,
/// so it needs no model or service beyond the API, and gives the same answer every time.
/// </summary>
public static partial class SourceSearch
{
    public const int PassageLength = 1_200;
    public const int MaxResults = 4;

    private static readonly HashSet<string> Stop =
    [
        "the", "and", "for", "with", "that", "this", "from", "are", "was", "were", "what", "which", "who", "how", "why", "when",
        "where", "into", "about", "there", "their", "they", "have", "has", "had", "not", "but", "any", "all", "can", "will",
        "would", "should", "does", "did", "its", "our", "your"
    ];

    /// <summary>The best passages for <paramref name="query"/>, most matching first; none when no passage shares a word with it.</summary>
    public static List<Passage> Search(IEnumerable<Source> sources, string query)
    {
        var terms = Words(query).Where(w => w.Length >= 3 && !Stop.Contains(w)).Select(Stem).Distinct().ToList();
        if (terms.Count == 0)
            return [];
        return sources
            .SelectMany(source =>
            {
                var passages = Passages(source.Text);
                return passages.Select((text, i) => (Passage: new Passage(source, i + 1, passages.Count, text), Score: Score(text, terms)));
            })
            .Where(p => p.Score > 0)
            .OrderByDescending(p => p.Score)
            .Take(MaxResults)
            .Select(p => p.Passage)
            .ToList();
    }

    /// <summary>Paragraphs packed into passages of about 1,200 characters; a longer paragraph is cut between words.</summary>
    public static List<string> Passages(string text)
    {
        var passages = new List<string>();
        var current = "";
        foreach (var paragraph in text.Split("\n\n").SelectMany(Pieces))
        {
            if (current.Length > 0 && current.Length + 2 + paragraph.Length > PassageLength)
            {
                passages.Add(current);
                current = "";
            }
            current = current.Length == 0 ? paragraph : $"{current}\n\n{paragraph}";
        }
        if (current.Length > 0)
            passages.Add(current);
        return passages;
    }

    private static IEnumerable<string> Pieces(string paragraph)
    {
        var rest = paragraph.Trim();
        while (rest.Length > PassageLength)
        {
            var cut = rest.LastIndexOf(' ', PassageLength);
            if (cut <= 0)
                cut = PassageLength;
            yield return rest[..cut].Trim();
            rest = rest[cut..].Trim();
        }
        if (rest.Length > 0)
            yield return rest;
    }

    /// <summary>A passage with more of the query's words beats one that repeats a single word.</summary>
    private static double Score(string passage, List<string> terms)
    {
        var counts = Words(passage).Select(Stem).GroupBy(w => w).ToDictionary(g => g.Key, g => g.Count());
        return terms.Sum(t => counts.TryGetValue(t, out var n) ? 1 + Math.Log(n) : 0);
    }

    private static IEnumerable<string> Words(string text) => Word().Matches(text.ToLowerInvariant()).Select(m => m.Value);

    /// <summary>A plural's "s" dropped, so "cranes" finds "crane".</summary>
    private static string Stem(string word) => word.Length > 3 && word.EndsWith('s') ? word[..^1] : word;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Word();
}
