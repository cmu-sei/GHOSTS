// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Enclaves;

/// <summary>A scenario's enclave: its slice spec as GraySpace takes it, the version last saved, and the last instance and job.</summary>
public record EnclaveView(JsonNode Spec, string? Version, string? InstanceId, string? JobId, string? UpdatedBy, DateTime? Updated)
{
    public static EnclaveView From(Enclave e) =>
        new(JsonNode.Parse(e.Spec)!, e.Version, e.InstanceId, e.JobId, e.Id == 0 ? null : e.UpdatedBy, e.Id == 0 ? null : e.Updated);
}

/// <summary>What GraySpace answered an enclave action, and the enclave after it.</summary>
public record EnclaveAnswer(EnclaveView Enclave, JsonNode? GraySpace);

public static partial class EnclaveRules
{
    /// <summary>The scenario's enclave, or a new one named from the scenario, stored once something changes it. Null when there's no such scenario.</summary>
    public static async Task<Enclave?> Find(BuilderDb db, int scenarioId, CancellationToken ct)
    {
        if (await db.Enclaves.FirstOrDefaultAsync(e => e.ScenarioId == scenarioId, ct) is { } enclave)
            return enclave;
        return await db.Scenarios.FindAsync([scenarioId], ct) is { } scenario
            ? new Enclave { ScenarioId = scenarioId, Spec = NewSpec(scenario).ToJsonString() }
            : null;
    }

    /// <summary>Records who changed the enclave and when, adding it if it's new. The caller saves.</summary>
    public static void Touch(BuilderDb db, Enclave enclave, HttpContext http)
    {
        if (enclave.Id == 0)
            db.Enclaves.Add(enclave);
        enclave.UpdatedBy = Caller.From(http).User;
        enclave.Updated = DateTime.UtcNow;
    }

    public static JsonObject Spec(Enclave enclave) => JsonNode.Parse(enclave.Spec)!.AsObject();

    /// <summary>The slice's slug; null when the enclave has no name.</summary>
    public static string? Slug(Enclave enclave) =>
        Spec(enclave)["slug"] is JsonValue v && v.TryGetValue(out string? slug) && slug.Trim().Length > 0 ? slug.Trim() : null;

    public static IResult Unnamed() => ErrorResults.BadRequest("ENCLAVE_NAME_REQUIRED", "Give the enclave a name under Enclave first.");

    /// <summary>Stores the draft on GraySpace as a version (the same one again when nothing changed) and records it.</summary>
    public static async Task<JsonNode?> Save(GraySpaceClient grayspace, Enclave enclave, string slug, CancellationToken ct)
    {
        var result = await grayspace.Json(HttpMethod.Put, $"slices/{Uri.EscapeDataString(slug)}", Spec(enclave), ct);
        enclave.Version = (string?)result?["version"];
        return result;
    }

    public static string? JobOf(JsonNode? accepted) => (string?)accepted?["job"]?["id"];

    /// <summary>A new enclave: the scenario's slug and name, and a hostname prefix from the slug.</summary>
    private static JsonObject NewSpec(Scenario scenario)
    {
        var slug = scenario.Slug.Length > 63 ? scenario.Slug[..63].TrimEnd('-') : scenario.Slug;
        if (slug == "base")
            slug = "base-enclave";   // GraySpace keeps "base" for its own slice
        var letters = NotPrefix().Replace(slug, "").TrimStart("0123456789".ToCharArray());
        return new JsonObject
        {
            ["apiVersion"] = "grayspace/v1",
            ["kind"] = "Slice",
            ["slug"] = slug,
            ["prefix"] = letters.Length == 0 ? "enclave" : letters[..Math.Min(8, letters.Length)],
            ["description"] = scenario.Name,
            ["owner"] = "scenario-builder",
            ["sites"] = new JsonArray(),
            ["hosts"] = new JsonArray(),
            ["role_vars"] = new JsonObject()
        };
    }

    [GeneratedRegex("[^a-z0-9]")]
    private static partial Regex NotPrefix();
}
