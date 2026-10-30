// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Outputs;

/// <summary>
/// GET /scenarios/{id}/documents/{library}/{template} — the document, rendered now from the scenario as its
/// audience may see it. Read as HTML (Markdown rendered); with <c>?download=true</c>, a <c>.html</c> or raw <c>.md</c> file.
/// </summary>
public class DocumentEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/documents/{library}/{template}",
                async (int id, string library, string template, bool? download, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
                {
                    if (await ScenarioState.Load(db, kinds, id, ct) is not { } state)
                        return ScenarioState.Missing(id);
                    var file = (await DocumentsEndpoint.Published(db, state.Scenario.KindId, ct)).FirstOrDefault(f => f.Slug == library);
                    if (file?.Templates.FirstOrDefault(t => t.Id == template) is not { } spec)
                        return ErrorResults.NotFound("DOCUMENT_UNKNOWN", $"No published library for this scenario's kind has a document {library}/{template}.");

                    var input = Rendering.Input(state, spec.Audience, file.Name, file.Version);
                    string text;
                    try
                    {
                        text = TemplateRenderer.Render(spec, input);
                    }
                    catch (TemplateError e)
                    {
                        return Rendering.Html(http, Rendering.ErrorPage(e), StatusCodes.Status422UnprocessableEntity);
                    }

                    if (download != true)
                        return Rendering.Html(http, TemplateRenderer.Page(spec, input, text));
                    var name = $"{state.Scenario.Slug}-{spec.Id}";
                    if (spec.Format == TemplateFormat.Markdown)
                        return Results.File(Encoding.UTF8.GetBytes(TemplateRenderer.MarkdownFile(input, text)), "text/markdown; charset=utf-8", $"{name}.md");
                    http.Response.Headers.ContentSecurityPolicy = Rendering.Policy;
                    return Results.File(Encoding.UTF8.GetBytes(TemplateRenderer.Page(spec, input, text)), "text/html; charset=utf-8", $"{name}.html");
                })
            .WithTags(tag);
}
