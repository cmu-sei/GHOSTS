// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace ScenarioBuilder.Api.Domain;

/// <summary>
/// The text of what a person gives as context: a file, or a fetched page. As in GHOSTS: PDF through PdfPig,
/// DOCX through OpenXml, HTML stripped of its tags, and plain text as it is.
/// </summary>
public static partial class SourceText
{
    /// <summary>The named HttpClient that fetches URL sources.</summary>
    public const string WebClient = "sources";

    public const int MaxCharacters = 1_000_000;
    public const long MaxFileBytes = 20 * 1024 * 1024;
    public const int MaxPageBytes = 5 * 1024 * 1024;

    public static readonly string[] FileTypes = [".pdf", ".docx", ".txt", ".md", ".csv", ".json", ".html", ".htm"];

    /// <summary>The file's text, or null for a type this doesn't read. A damaged file throws.</summary>
    public static string? FromFile(byte[] data, string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => Pdf(data),
        ".docx" => Docx(data),
        ".html" or ".htm" => FromHtml(Decode(data)).Text,
        ".txt" or ".md" or ".csv" or ".json" => Decode(data),
        _ => null
    };

    public static string Pdf(byte[] data)
    {
        using var document = PdfDocument.Open(data);
        // Words, not page.Text: a page's letters often carry no space glyphs between words.
        return string.Join("\n\n", document.GetPages().Select(page => string.Join(" ", page.GetWords().Select(w => w.Text))));
    }

    /// <summary>One line per paragraph; the document's own InnerText runs them together.</summary>
    public static string Docx(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body is null ? "" : string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText));
    }

    /// <summary>A page's readable text and its title: no scripts, styles or tags, entities decoded, one block per line.</summary>
    public static (string Text, string? Title) FromHtml(string html)
    {
        var title = Title().Match(html) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
        var text = Hidden().Replace(html, " ");
        text = Block().Replace(text, "\n");
        text = Tag().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        var lines = text.Split('\n').Select(l => Spaces().Replace(l, " ").Trim());
        return (Normalize(string.Join("\n", lines)), string.IsNullOrEmpty(title) ? null : title);
    }

    public static string Decode(byte[] data)
    {
        using var reader = new StreamReader(new MemoryStream(data), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>Unix line ends, no more than one blank line in a row, no outer whitespace.</summary>
    public static string Normalize(string text) => BlankLines().Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "\n\n").Trim();

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Title();

    [GeneratedRegex(@"<(script|style|noscript|head|template)\b.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<(br|/?(p|div|li|ul|ol|tr|table|h[1-6]|section|article|header|footer|blockquote|pre))\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Block();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t\f\v\u00A0]+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"\n\s*\n(\s*\n)*")]
    private static partial Regex BlankLines();
}
