// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Json.Schema;

namespace Ghosts.Api.Infrastructure.ScenarioDocuments;

/// <summary>
/// Validates a scenario document and returns findings. It writes nothing, decides nothing, and never
/// throws on a bad document: a caller reads the findings and an import refuses on any error.
///
/// Tier 1 is the schema, evaluated in-process by JsonSchema.Net against the embedded copy of
/// schemas/scenario-document/v1. It is not a shell-out to the node tool: the shipped image is
/// aspnet:10.0 and has no node, so a shell-out is a validator that does not run where it matters.
/// The node tool stays the reference — schemas/scenario-document/tests/crosscheck-expected.json
/// records ajv's verdict and failing paths for the examples, and a test holds this code to them.
///
/// Tier 2 is referential and lives in ScenarioDocumentReferentialValidator. It runs only when tier 1
/// found nothing: tier 2 reads a document, and a malformed document cannot be read.
/// Tier 3 (doctrinal, model-assisted) and tier 5 (deployed verification) are not built.
/// </summary>
public static class ScenarioDocumentValidator
{
    /// <summary>Tier 1 and tier 2, with no network. The n8n registration check reports itself skipped.</summary>
    public static ScenarioValidationResult Validate(JsonNode document) =>
        ValidateAsync(document, null, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Tier 1 and tier 2. A client factory lets tier 2 ask n8n whether each workflow ref is
    /// registered; without one, or with n8n unreachable, that check reports itself skipped.
    /// </summary>
    public static async Task<ScenarioValidationResult> ValidateAsync(
        JsonNode document, IHttpClientFactory clients, CancellationToken ct)
    {
        if (document is not JsonObject doc)
        {
            return new ScenarioValidationResult([ScenarioFinding.Err(
                1, "SCHEMA_NOT_AN_OBJECT", string.Empty, "A scenario document must be a JSON object.")]);
        }

        // A document written against a schema this build does not have cannot be judged by this
        // schema, so the version is the one thing checked before anything else.
        if (VersionFinding(doc) is { } version) return new ScenarioValidationResult([version]);

        var tier1 = Tier1(doc).ToList();
        if (tier1.Count > 0) return new ScenarioValidationResult(Ordered(tier1));

        var tier2 = await ScenarioDocumentReferentialValidator.RunAsync(doc, clients, ct);
        return new ScenarioValidationResult(Ordered([.. tier1, .. tier2]));
    }

    /// <summary>Findings sorted so a caller reads the document in order: by tier, then by path.</summary>
    internal static IReadOnlyList<ScenarioFinding> Ordered(IEnumerable<ScenarioFinding> findings) =>
        findings.OrderBy(f => f.Tier).ThenBy(f => f.Path, StringComparer.Ordinal).ThenBy(f => f.Code, StringComparer.Ordinal).ToList();

    private static ScenarioFinding VersionFinding(JsonObject doc)
    {
        var stated = doc["schemaVersion"] as JsonValue;
        if (stated == null || !stated.TryGetValue<string>(out var version) || string.IsNullOrEmpty(version))
        {
            return ScenarioFinding.Err(1, "SCHEMA_VERSION_MISSING", "/schemaVersion",
                "The document does not say which schema version it was written against.",
                $"Add \"schemaVersion\": \"{ScenarioDocumentSchema.Version}\".");
        }
        if (ScenarioDocumentSchema.SupportedVersions.Contains(version)) return null;

        return ScenarioFinding.Err(1, "SCHEMA_VERSION_UNSUPPORTED", "/schemaVersion",
            $"Schema version {version} is not one this build can read ({string.Join(", ", ScenarioDocumentSchema.SupportedVersions)}).",
            "A newer document needs a newer GHOSTS; an older one needs the reader that shipped with it.");
    }

    // ───────────────────────── tier 1: the schema ─────────────────────────

    /// <summary>
    /// Keywords that only say "something below me failed". The finding belongs to the value that
    /// actually failed, which is what the child result carries, so these are dropped.
    /// </summary>
    private static readonly HashSet<string> Structural = new(StringComparer.Ordinal)
    {
        "properties", "patternProperties", "additionalProperties", "items", "prefixItems",
        "contains", "allOf", "dependentSchemas", "propertyNames", "$ref", "$dynamicRef",
        "unevaluatedProperties", "unevaluatedItems", "not", "if", "then", "else"
    };

    /// <summary>Keywords whose children are alternatives, not requirements.</summary>
    private static readonly HashSet<string> Branching = new(StringComparer.Ordinal) { "anyOf", "oneOf" };

    private static IEnumerable<ScenarioFinding> Tier1(JsonObject doc)
    {
        using var parsed = JsonDocument.Parse(doc.ToJsonString());
        // Hierarchical, not List: a flat list keeps the results of subschemas the document did not
        // have to satisfy, and their errors are not defects. The tree says which are which.
        var results = ScenarioDocumentSchema.Schema.Evaluate(parsed.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Hierarchical
        });
        return Flatten(results).Distinct();
    }

    private static IEnumerable<ScenarioFinding> Flatten(EvaluationResults node)
    {
        // A valid subtree has nothing to say, even when its parent failed for another reason.
        if (node.IsValid) yield break;

        foreach (var error in node.Errors ?? [])
        {
            var keyword = string.IsNullOrEmpty(error.Key) ? LastKeyword(node) : error.Key;
            if (Structural.Contains(keyword) && !string.IsNullOrEmpty(error.Key)) continue;
            if (Branching.Contains(keyword)) continue; // reported below, with the alternatives named
            yield return ScenarioFinding.Err(1, Code(keyword), Pointer(node), Message(keyword, error.Value));
        }

        foreach (var group in (node.Details ?? []).GroupBy(child => Relative(node, child)))
        {
            if (Branching.Contains(group.Key))
            {
                // Every alternative failed, so the value satisfies none of them. One finding naming
                // all of them beats one per alternative, which reads as four unrelated complaints.
                if (!(node.Errors?.ContainsKey(group.Key) ?? false)) continue;
                var alternatives = group.SelectMany(Flatten).Select(f => f.Message).Distinct().ToList();
                yield return ScenarioFinding.Err(1, Code(group.Key), Pointer(node),
                    alternatives.Count > 0
                        ? $"None of the allowed shapes fit: {string.Join(" / ", alternatives)}"
                        : "The value fits none of the allowed shapes.");
                continue;
            }
            foreach (var child in group)
            foreach (var finding in Flatten(child))
            {
                yield return finding;
            }
        }
    }

    /// <summary>The first schema keyword on the child's evaluation path below the parent's.</summary>
    private static string Relative(EvaluationResults parent, EvaluationResults child)
    {
        var from = parent.EvaluationPath.ToString();
        var to = child.EvaluationPath.ToString();
        if (!to.StartsWith(from, StringComparison.Ordinal)) return string.Empty;
        return to[from.Length..].TrimStart('/').Split('/').FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// The keyword a result with no error key came from. A false schema — which is what
    /// "additionalProperties": false compiles to — reports its error under the empty key.
    /// </summary>
    private static string LastKeyword(EvaluationResults node) =>
        node.EvaluationPath.ToString().Split('/').LastOrDefault(s => s.Length > 0 && !s.All(char.IsDigit))
        ?? "schema";

    private static string Pointer(EvaluationResults node) => node.InstanceLocation.ToString();

    /// <summary>SCHEMA_ADDITIONAL_PROPERTIES from additionalProperties, SCHEMA_MIN_LENGTH from minLength.</summary>
    private static string Code(string keyword)
    {
        var sb = new StringBuilder("SCHEMA_");
        foreach (var c in keyword)
        {
            if (char.IsUpper(c)) sb.Append('_');
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString().Replace("$", string.Empty);
    }

    /// <summary>The library's message, except where the keyword deserves a sentence of its own.</summary>
    private static string Message(string keyword, string message) => keyword switch
    {
        "additionalProperties" => "The schema does not define this property; it would be dropped silently.",
        "dependentRequired" => $"{message} (a value elsewhere in this object makes it required)",
        _ => message
    };
}
