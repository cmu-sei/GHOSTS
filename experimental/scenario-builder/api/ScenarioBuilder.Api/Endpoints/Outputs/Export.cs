// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Outputs;

/// <summary>
/// GET /scenarios/{id}/export — the pull API's document, as <c>&lt;slug&gt;.scenario.json</c>; with
/// <c>?format=markdown</c>, the same scenario as a readable <c>&lt;slug&gt;.scenario.md</c>.
/// </summary>
public class ExportEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapGet("/scenarios/{id:int}/export",
                async (int id, string? format, KindCatalog kinds, BuilderDb db, IOptions<JsonOptions> json, CancellationToken ct) =>
                {
                    if (format is not (null or "json" or "markdown"))
                        return ErrorResults.BadRequest("FORMAT_UNKNOWN", $"There is no export format '{format}'. Formats: json, markdown.");
                    if (await ScenarioState.Load(db, kinds, id, ct) is not { } s)
                        return ScenarioState.Missing(id);
                    if (format == "markdown")
                        return Results.File(Encoding.UTF8.GetBytes(ScenarioMarkdown.Build(s.Kind, s.Scenario, s.Decisions, s.Approvals)),
                            "text/markdown; charset=utf-8", $"{s.Scenario.Slug}.scenario.md");
                    var document = ScenarioDocument.Build(s.Kind, s.Scenario, s.Decisions, s.Approvals);
                    return Results.File(JsonSerializer.SerializeToUtf8Bytes(document, json.Value.SerializerOptions),
                        "application/json", $"{s.Scenario.Slug}.scenario.json");
                })
            .WithTags(tag);
}
