// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using Json.Schema;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// The scenario-document schema, embedded in the assembly. schemas/scenario-document/v1/ is the
/// source; the build copies it in, so the validator runs in the shipped aspnet image with no
/// repository tree and no node. Two views of the same text: a compiled JsonSchema for tier 1, and
/// the raw JSON for canonical form, which needs the key order, the required lists and the defaults.
/// </summary>
public static class ScenarioDocumentSchema
{
    /// <summary>The version an export writes.</summary>
    public const string Version = "1.1.0";

    /// <summary>Versions an import reads. 1.1.0 only declares defaults 1.0.0 left implicit.</summary>
    public static readonly IReadOnlyList<string> SupportedVersions = ["1.0.0", "1.1.0"];

    private const string Resource = "Ghosts.Api.ScenarioDocuments.scenario-document.schema.json";

    private static readonly Lazy<string> Text = new(() => Read(Resource));
    private static readonly Lazy<JsonSchema> Compiled = new(() => JsonSchema.FromText(Text.Value));
    private static readonly Lazy<JsonObject> Raw = new(() => (JsonObject)JsonNode.Parse(Text.Value));

    public static JsonSchema Schema => Compiled.Value;

    public static JsonObject Node => Raw.Value;

    /// <summary>Resolves a local $ref the way canonical form needs it: the target merged under the node.</summary>
    public static JsonObject Resolve(JsonNode node)
    {
        if (node is not JsonObject o) return null;
        if (o["$ref"]?.GetValue<string>() is not { } reference || !reference.StartsWith("#/", StringComparison.Ordinal))
            return o;

        JsonNode target = Node;
        foreach (var segment in reference[2..].Split('/'))
        {
            target = target?[segment];
        }
        if (target is not JsonObject resolved) return o;

        var merged = new JsonObject();
        foreach (var kv in resolved) merged[kv.Key] = kv.Value?.DeepClone();
        foreach (var kv in o)
        {
            if (kv.Key == "$ref") continue;
            merged[kv.Key] = kv.Value?.DeepClone();
        }
        return merged;
    }

    internal static string Read(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded resource {resource} is missing from the assembly");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
