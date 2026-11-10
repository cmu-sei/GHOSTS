// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
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
                async (int id, string? format, ScenarioTypeCatalog scenarioTypes, BuilderDb db, IOptions<JsonOptions> json, CancellationToken ct) =>
                {
                    if (format is not (null or "json" or "markdown"))
                        return ErrorResults.BadRequest("FORMAT_UNKNOWN", $"There is no export format '{format}'. Formats: json, markdown.");
                    if (await ScenarioState.Load(db, scenarioTypes, id, ct) is not { } s)
                        return ScenarioState.Missing(id);
                    if (format == "markdown")
                        return Results.File(Encoding.UTF8.GetBytes(ScenarioMarkdown.Build(s.ScenarioType, s.Scenario, s.Decisions)),
                            "text/markdown; charset=utf-8", $"{s.Scenario.Slug}.scenario.md");
                    var document = ScenarioDocument.Build(s.ScenarioType, s.Scenario, s.Decisions, await db.Enclaves.FirstOrDefaultAsync(e => e.ScenarioId == id, ct));
                    return Results.File(JsonSerializer.SerializeToUtf8Bytes(document, json.Value.SerializerOptions),
                        "application/json", $"{s.Scenario.Slug}.scenario.json");
                })
            .WithTags(tag);
}
