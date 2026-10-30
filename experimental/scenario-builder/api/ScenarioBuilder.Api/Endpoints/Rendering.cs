// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using ScenarioBuilder.Api.Domain;

namespace ScenarioBuilder.Api.Endpoints;

/// <summary>What the preview and the documents share: a scenario filtered for an audience, served under a strict CSP.</summary>
public static class Rendering
{
    /// <summary>A rendered page can't run script or load anything, so a template's markup can't act in the builder's origin.</summary>
    public const string Policy = "default-src 'none'; style-src 'unsafe-inline'; img-src data:";

    public static RenderInput Input(ScenarioState s, Audience audience, string libraryName, int libraryVersion)
    {
        var document = ScenarioDocument.Build(s.Kind, s.Scenario, s.Decisions, s.Approvals);
        return new RenderInput(
            AudienceFilter.For(document, s.Kind, audience),
            s.Kind.AllQuestions.ToDictionary(q => q.Id, q => q.Label ?? q.Id),
            s.Kind, s.Scenario.Name, libraryName, libraryVersion,
            document["findings"]!.AsArray().Count, DateTime.UtcNow);
    }

    public static IResult Html(HttpContext http, string html, int status = StatusCodes.Status200OK)
    {
        http.Response.Headers.ContentSecurityPolicy = Policy;
        return Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, status);
    }

    /// <summary>What a reader sees in place of a document whose template fails. The other documents still work.</summary>
    public static string ErrorPage(TemplateError error) => $"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>{TemplateRenderer.Escape(error.Template)} couldn't be made</title></head>
        <body style="font: 15px/1.55 sans-serif; max-width: 72ch; margin: 40px auto; padding: 0 24px; color: #1c2230">
        <h1 style="font-size: 22px">{TemplateRenderer.Escape(error.Template)} couldn't be made</h1>
        <p>{TemplateRenderer.Escape(error.Message)}</p>
        <p>An admin can fix the template in Admin and publish the library again.</p>
        </body>
        </html>
        """;
}
