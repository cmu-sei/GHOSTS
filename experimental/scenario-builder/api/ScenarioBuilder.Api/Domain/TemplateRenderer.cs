// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Markdig;
using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;

namespace ScenarioBuilder.Api.Domain;

public enum TemplateFormat { Html, Markdown }

/// <summary>One document's Liquid source. The audience decides what it is given (<see cref="AudienceFilter"/>).</summary>
public record TemplateSpec(string Id, string Name, Audience Audience, TemplateFormat Format, string Body);

/// <summary>What a template is rendered against. <see cref="Scenario"/> is already filtered for its audience.</summary>
public record RenderInput(
    JsonObject Scenario,
    IReadOnlyDictionary<string, string> Labels,
    Kind Kind,
    string ScenarioName,
    string LibraryName,
    int LibraryVersion,
    int OpenFindings,
    DateTime Generated);

/// <summary>A template that won't parse or won't run, with the template's name and the 1-based line (0 when none).</summary>
public sealed class TemplateError(string template, int line, string message)
    : Exception(line > 0 ? $"{template}, line {line}: {message}" : $"{template}: {message}")
{
    public string Template { get; } = template;
    public int Line { get; } = line;
}

/// <summary>
/// Fills templates with Liquid, sandboxed: no files or network, loops capped, recursion capped, two seconds at most.
/// An HTML template gets every string HTML-escaped, so an answer can't inject markup; Markdown gets values as they are.
/// </summary>
public static partial class TemplateRenderer
{
    public const int MaxBody = 200_000;

    /// <summary>Null when the template parses and isn't too big; otherwise what's wrong, naming the template and line.</summary>
    public static string? Check(TemplateSpec t)
    {
        if (t.Body.Length > MaxBody)
            return $"{t.Name}: the template is {t.Body.Length:N0} characters; the limit is 200,000.";
        var parsed = Template.ParseLiquid(t.Body);
        return parsed.HasErrors ? $"{t.Name}, line {parsed.Messages[0].Span.Start.Line + 1}: {parsed.Messages[0].Message}" : null;
    }

    public static string Render(TemplateSpec t, RenderInput input)
    {
        if (t.Body.Length > MaxBody)
            throw new TemplateError(t.Name, 0, "the template is over 200,000 characters.");
        var template = Template.ParseLiquid(t.Body);
        if (template.HasErrors)
            throw new TemplateError(t.Name, template.Messages[0].Span.Start.Line + 1, template.Messages[0].Message);

        var html = t.Format == TemplateFormat.Html;
        string Text(string s) => html ? Escape(s) : s;
        var labels = new ScriptObject();
        foreach (var (id, label) in input.Labels)
            labels[id] = Text(label);
        var globals = new ScriptObject
        {
            ["scenario"] = Value(input.Scenario, html),
            ["labels"] = labels,
            ["kind"] = new ScriptObject { ["id"] = input.Kind.Id, ["name"] = Text(input.Kind.Name), ["version"] = input.Kind.Version },
            ["library"] = new ScriptObject { ["name"] = Text(input.LibraryName), ["version"] = input.LibraryVersion },
            ["template"] = new ScriptObject { ["name"] = Text(t.Name), ["audience"] = JsonNamingPolicy.CamelCase.ConvertName(t.Audience.ToString()) },
            ["generated"] = input.Generated.ToString("O", CultureInfo.InvariantCulture),
            ["draft"] = input.OpenFindings > 0
        };

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var context = new LiquidTemplateContext { LoopLimit = 10_000, RecursiveLimit = 50, StrictVariables = false, EnableRelaxedTargetAccess = true, CancellationToken = timeout.Token };
        // Scriban's regex functions run inside one expression, where the time limit can't stop them; Liquid has none.
        context.BuiltinObject.Remove("regex");
        context.PushGlobal(globals);
        try
        {
            return template.Render(context);
        }
        catch (ScriptRuntimeException) when (timeout.IsCancellationRequested)
        {
            throw new TemplateError(t.Name, 0, "it ran longer than 2 seconds.");
        }
        catch (ScriptRuntimeException e)
        {
            throw new TemplateError(t.Name, e.Span.Start.Line + 1, e.OriginalMessage ?? e.Message);
        }
    }

    /// <summary>
    /// The document as a page. Markdown is rendered to HTML first. A template that writes its own page keeps it and
    /// gets only the draft banner; anything else goes in the house page, with its title, banner and footer.
    /// </summary>
    public static string Page(TemplateSpec t, RenderInput input, string rendered)
    {
        var body = t.Format == TemplateFormat.Markdown ? Markdown.ToHtml(rendered, MarkdownText) : rendered;
        var banner = input.OpenFindings > 0
            ? $"<div class=\"draft\">Draft: {input.OpenFindings} {(input.OpenFindings == 1 ? "finding" : "findings")} open.</div>"
            : string.Empty;

        if (t.Format == TemplateFormat.Html && OwnPage().IsMatch(body))
        {
            var tag = BodyTag().Match(body);
            return tag.Success ? body.Insert(tag.Index + tag.Length, banner) : banner + body;
        }

        var date = input.Generated.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var library = Escape(input.LibraryName);
        // Version 0 is an admin's preview of an unpublished draft.
        var footer = input.LibraryVersion > 0
            ? $"Made from {library}, version {input.LibraryVersion}, on {date}."
            : $"Previewed from the draft of {library} on {date}.";
        return $"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{Escape(t.Name)} for {Escape(input.ScenarioName)}</title>
            <style>{HouseStyle}</style>
            </head>
            <body>
            <main>
            {banner}
            {body}
            </main>
            <footer>{footer}</footer>
            </body>
            </html>
            """;
    }

    /// <summary>The <c>.md</c> download: the rendered text, after a draft line while findings are open.</summary>
    public static string MarkdownFile(RenderInput input, string rendered) =>
        input.OpenFindings > 0
            ? $"> Draft: {input.OpenFindings} {(input.OpenFindings == 1 ? "finding" : "findings")} open.\n\n{rendered}"
            : rendered;

    /// <summary>Escapes only what markup needs, so any other character, accented or not Latin, comes through as itself.</summary>
    public static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    private static object? Value(JsonNode? node, bool html)
    {
        switch (node)
        {
            case JsonObject obj:
                var o = new ScriptObject();
                foreach (var (key, child) in obj)
                    o[key] = Value(child, html);
                return o;
            case JsonArray array:
                var a = new ScriptArray();
                foreach (var child in array)
                    a.Add(Value(child, html));
                return a;
            case JsonValue value:
                if (value.TryGetValue<string>(out var s))
                    return html ? Escape(s) : s;
                if (value.TryGetValue<bool>(out var b))
                    return b;
                if (value.TryGetValue<long>(out var l))
                    return l;
                return value.TryGetValue<double>(out var d) ? d : value.ToJsonString();
            default:
                return null;
        }
    }

    /// <summary>Markdown templates get answers as they are, so any HTML in them is shown as text, never as markup.</summary>
    private static readonly MarkdownPipeline MarkdownText = new MarkdownPipelineBuilder().DisableHtml().Build();

    // A readable printed page: a system face, a measure under 80 characters, quiet tables.
    private const string HouseStyle = """
        @page { margin: 18mm 16mm; }
        body { margin: 0; color: #1c2230; font: 15px/1.55 "IBM Plex Sans", "Helvetica Neue", Arial, sans-serif; }
        main { max-width: 72ch; margin: 40px auto; padding: 0 24px; }
        h1 { font-size: 26px; line-height: 1.2; margin: 0 0 8px; }
        h2 { font-size: 18px; margin: 32px 0 8px; }
        h3 { font-size: 15px; margin: 20px 0 6px; }
        table { width: 100%; border-collapse: collapse; margin: 12px 0; font-size: 13px; }
        th, td { text-align: left; vertical-align: top; padding: 6px 8px; border-bottom: 1px solid #e3e6eb; }
        th { color: #6b7385; font-weight: 500; }
        blockquote { margin: 12px 0; padding: 8px 14px; border-left: 3px solid #cfd4dc; color: #4a5263; }
        .draft { margin: 0 0 20px; padding: 8px 12px; border-radius: 6px; background: #fff3df; color: #a3600a; font-size: 13px; }
        footer { max-width: 72ch; margin: 0 auto 40px; padding: 12px 24px 0; color: #9aa1b0; font-size: 12px; border-top: 1px solid #e3e6eb; }
        @media print { main { margin: 0 auto; } footer { position: fixed; bottom: 0; left: 0; right: 0; } }
        """;

    [GeneratedRegex("<html", RegexOptions.IgnoreCase)]
    private static partial Regex OwnPage();

    [GeneratedRegex("<body[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BodyTag();
}
