// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Admin;

/// <summary>The template editor's unsaved body. The library, when given, names the footer.</summary>
public record PreviewTemplate(int ScenarioId, Audience Audience, TemplateFormat Format, string? Body, string? Name = null, int? LibraryId = null);

/// <summary>POST /admin/preview — a template rendered on a scenario exactly as its audience would get it.</summary>
public class PreviewEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/admin/preview", async (PreviewTemplate body, HttpContext http, KindCatalog kinds, BuilderDb db, CancellationToken ct) =>
            {
                if (!Enum.IsDefined(body.Audience) || !Enum.IsDefined(body.Format))
                    return ErrorResults.BadRequest("TEMPLATE_INVALID", "Use the audience participants, facilitators or designers, and the format html or markdown.");
                var state = await ScenarioState.Load(db, kinds, body.ScenarioId, ct);
                if (state is null)
                    return ScenarioState.Missing(body.ScenarioId);
                var library = body.LibraryId is { } libraryId ? await db.Libraries.FindAsync([libraryId], ct) : null;

                var spec = new TemplateSpec("preview", string.IsNullOrWhiteSpace(body.Name) ? "Preview" : body.Name, body.Audience, body.Format, body.Body ?? "");
                var input = Rendering.Input(state, spec.Audience, library?.Name ?? "an unsaved library", 0);
                try
                {
                    return Rendering.Html(http, TemplateRenderer.Page(spec, input, TemplateRenderer.Render(spec, input)));
                }
                catch (TemplateError e)
                {
                    return ErrorResults.BadRequest("TEMPLATE_INVALID", e.Message);
                }
            })
            .AddEndpointFilter<AdminOnly>().WithTags(tag);
}
