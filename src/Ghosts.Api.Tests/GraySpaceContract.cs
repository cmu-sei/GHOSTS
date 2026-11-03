// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Ghosts.Api.Tests;

/// <summary>
/// GraySpace's published contract (GraySpace/*.json, Terrain API 1.3.0), for checking what GHOSTS sends GraySpace and
/// what its stub answers. A component of the OpenAPI document is checked through a schema whose $defs are the
/// document's components, so its references resolve without walking OpenAPI's own keywords.
/// </summary>
internal static class GraySpaceContract
{
    // Lazy, so each schema is built once: JsonSchema.Net registers a schema's $id globally and refuses a second one.
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> Compiled = new();

    private static JsonSchema Get(string key, Func<JsonSchema> build) => Compiled.GetOrAdd(key, _ => new Lazy<JsonSchema>(build)).Value;

    /// <summary>Why the JSON is not a valid instance of the named schema in components/schemas; empty when it is.</summary>
    public static List<string> Errors(string component, string json) =>
        Evaluate(Get(component, () => Component(component)), json);

    /// <summary>Why the JSON is not a valid slice spec; empty when it is.</summary>
    public static List<string> SliceErrors(string json) =>
        Evaluate(Get("$slice", () => JsonSchema.FromText(File.ReadAllText(Path.Combine(Dir(), "grayspace-slice-v1.json")))), json);

    /// <summary>Why the JSON is not a valid /api/v1/events message; empty when it is.</summary>
    public static List<string> EventErrors(string json) =>
        Evaluate(Get("$events", () => JsonSchema.FromText(File.ReadAllText(Path.Combine(Dir(), "grayspace-events-v1.json")))), json);

    private static JsonSchema Component(string name)
    {
        var openApi = JsonNode.Parse(File.ReadAllText(Path.Combine(Dir(), "grayspace-v1.json")))!;
        var defs = openApi["components"]!["schemas"]!.ToJsonString().Replace("#/components/schemas/", "#/$defs/");
        var wrapper = $$"""{"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"https://grayspace/openapi/{{name}}","$defs":{{defs}},"$ref":"#/$defs/{{name}}"}""";
        return JsonSchema.FromText(wrapper);
    }

    private static List<string> Evaluate(JsonSchema schema, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = schema.Evaluate(doc.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];
        return (result.Details ?? []).Where(d => d.Errors is { Count: > 0 })
            .Select(d => $"{d.InstanceLocation}: {string.Join("; ", d.Errors!.Values)}").Distinct().ToList();
    }

    private static string Dir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Ghosts.Api.Tests", "GraySpace");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("src/Ghosts.Api.Tests/GraySpace not found above the test binaries");
    }
}
