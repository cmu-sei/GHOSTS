using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Ghosts.Tools.Mcp;

[McpServerToolType]
public sealed class GhostsTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [McpServerTool(Name = "api_get_base_url", ReadOnly = true, OpenWorld = false)]
    [Description("Returns the configured GHOSTS API base URL used by this MCP server.")]
    public static string GetGhostsApiBaseUrl()
    {
        return GhostsApiClient.BaseUrl;
    }

    [McpServerTool(Name = "api_check_connection", ReadOnly = true, OpenWorld = true)]
    [Description("Actively checks the live GHOSTS API connection and returns the result.")]
    public static async Task<string> CheckGhostsApiAsync(CancellationToken ct)
    {
        try
        {
            using var client = GhostsApiClient.Create();
            using var response = await client.GetAsync("/swagger/v9/swagger.json", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return JsonSerializer.Serialize(new
            {
                GhostsApiClient.BaseUrl,
                ok = response.IsSuccessStatusCode,
                status = (int)response.StatusCode,
                reason = response.ReasonPhrase,
                preview = body.Length > 500 ? body[..500] : body
            }, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            return ApiError("check_ghosts_api", ex);
        }
    }

    [McpServerTool(Name = "machine_list", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns the actual current list of GHOSTS machines now. Use this when the user asks to list, show, count, or inspect machines; return the tool result, not example code.")]
    public static async Task<string> ListMachinesAsync(
        [Description("Maximum number of machines to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync("/api/machines/list", take, ct);
    }

    [McpServerTool(Name = "machine_get_by_id", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns one GHOSTS machine record now, including recent history when available.")]
    public static async Task<string> GetMachineAsync(
        [Description("GHOSTS machine GUID.")] Guid machineId,
        CancellationToken ct = default)
    {
        return await GetJsonAsync($"/api/machines/{machineId}", null, ct);
    }

    [McpServerTool(Name = "npc_list", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns the current list of generated GHOSTS NPC IDs and names now.")]
    public static async Task<string> ListNpcsAsync(
        [Description("Maximum number of NPCs to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync("/api/npcs/list", take, ct);
    }

    [McpServerTool(Name = "npc_get_by_id", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns one GHOSTS NPC record now, including its full persona profile.")]
    public static async Task<string> GetNpcAsync(
        [Description("GHOSTS NPC GUID.")] Guid npcId,
        CancellationToken ct = default)
    {
        return await GetJsonAsync($"/api/npcs/{npcId}", null, ct);
    }

    [McpServerTool(Name = "scenario_list", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns the current list of GHOSTS scenarios now.")]
    public static async Task<string> ListScenariosAsync(
        [Description("Maximum number of scenarios to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync("/api/scenarios", take, ct);
    }

    // ───────────── scenario documents: the authoring agent's only tools that reach the API ─────────────
    //
    // A scenario document is one exercise as a single versioned JSON file (schemas/scenario-document).
    // Validate writes nothing, ever, so it is safe to call on a half-finished draft; import is the only
    // one of the four that writes, and it refuses on any error-severity finding. There is deliberately
    // no tool here that edits a scenario row: an authoring agent changes the document and imports it.

    [McpServerTool(Name = "scenario_document_validate", ReadOnly = true, OpenWorld = true)]
    [Description("Validates a GHOSTS scenario document and returns the findings. Writes nothing, ever, so it is safe on a draft. With dryRun it also loads and compiles the document inside a transaction that is always rolled back, adding tier-4 findings. Call this before import and after every edit.")]
    public static async Task<string> ValidateScenarioDocumentAsync(
        [Description("The scenario document as a JSON object (schema 1.1.0).")] string document,
        [Description("When true, also load and compile the document in a rolled-back transaction and report what that found.")] bool dryRun = false,
        CancellationToken ct = default)
    {
        return await PostDocumentAsync($"/api/scenarios/validate?dryRun={(dryRun ? "true" : "false")}", document, ct);
    }

    [McpServerTool(Name = "scenario_document_import", Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Creates a GHOSTS scenario from a scenario document. Runs the same validator as scenario_document_validate and refuses, writing nothing, on any finding of severity error. On success returns the new scenario id; on refusal returns the findings and no id.")]
    public static async Task<string> ImportScenarioDocumentAsync(
        [Description("The scenario document as a JSON object (schema 1.1.0).")] string document,
        CancellationToken ct = default)
    {
        return await PostDocumentAsync("/api/scenarios/import", document, ct);
    }

    [McpServerTool(Name = "scenario_document_export", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches one GHOSTS scenario as a canonical scenario document: no database ids, no timestamps, no run state. Two exports of an unchanged scenario are byte-identical, and the output is valid input to scenario_document_import.")]
    public static async Task<string> ExportScenarioDocumentAsync(
        [Description("GHOSTS scenario id.")] int scenarioId,
        CancellationToken ct = default)
    {
        return await GetJsonAsync($"/api/scenarios/{scenarioId}/document", null, ct);
    }

    [McpServerTool(Name = "attack_technique_lookup", ReadOnly = true, OpenWorld = false)]
    [Description("Resolves MITRE ATT&CK techniques by id or by a fragment of a name, from the index GHOSTS validates against. Returns id, name, domains, and whether MITRE revoked or deprecated it. Use this for every technique id that goes into a document; never write one from memory.")]
    public static async Task<string> LookupAttackTechniqueAsync(
        [Description("An ATT&CK technique id such as T1566.002, an id prefix, or part of a technique name such as \"spearphishing\".")] string query,
        [Description("Maximum number of matches to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync($"/api/attack/index/techniques?q={Uri.EscapeDataString(query ?? string.Empty)}&take={take}", null, ct);
    }

    [McpServerTool(Name = "attack_group_lookup", ReadOnly = true, OpenWorld = false)]
    [Description("Resolves MITRE ATT&CK intrusion sets (groups) by id, by a fragment of the primary name, or by a fragment of a known alias, from the same corpus attack_technique_lookup uses. Returns id, name, aliases, domains, and whether MITRE revoked or deprecated it. Use this before naming a real adversary in a document (ELICITATION.md E3); never write a group id from memory.")]
    public static async Task<string> LookupAttackGroupAsync(
        [Description("An ATT&CK group id such as G0034, an id prefix, or part of a name or alias such as \"sandworm\" or \"voodoo bear\".")] string query,
        [Description("Maximum number of matches to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync($"/api/attack/index/groups?q={Uri.EscapeDataString(query ?? string.Empty)}&take={take}", null, ct);
    }

    [McpServerTool(Name = "browser_timeline_build", ReadOnly = true, OpenWorld = false)]
    [Description("Builds a browser timeline JSON payload without sending it to GHOSTS.")]
    public static string BuildBrowserTimelineJson(
        [Description("URL the client should browse to.")] string url,
        [Description("Browser handler name, such as BrowserChrome, BrowserFirefox, or BrowserEdge.")] string browser = "BrowserChrome",
        [Description("Whether the handler loops.")] bool loop = false,
        [Description("Delay after the browse command in milliseconds.")] int delayAfterMs = 30000)
    {
        var timeline = BrowserTimelineFactory.Create(url, browser, loop, delayAfterMs);
        return JsonSerializer.Serialize(timeline, JsonOptions);
    }

    [McpServerTool(Name = "browser_timeline_send", Destructive = false, Idempotent = false, OpenWorld = true)]
    [Description("Immediately sends a browser timeline update to a GHOSTS machine and returns the API response.")]
    public static async Task<string> SendBrowserTimelineAsync(
        [Description("GHOSTS machine GUID that should receive the timeline.")] Guid machineId,
        [Description("URL the client should browse to.")] string url,
        [Description("Browser handler name, such as BrowserChrome, BrowserFirefox, or BrowserEdge.")] string browser = "BrowserChrome",
        [Description("Whether the handler loops.")] bool loop = false,
        [Description("Delay after the browse command in milliseconds.")] int delayAfterMs = 30000,
        [Description("When true, replaces the default timeline. When false, sends a partial timeline for immediate execution.")] bool replaceDefaultTimeline = false,
        CancellationToken ct = default)
    {
        var payload = new
        {
            machineId,
            type = replaceDefaultTimeline ? "Timeline" : "TimelinePartial",
            activeUtc = DateTime.UtcNow,
            status = "Active",
            update = BrowserTimelineFactory.Create(url, browser, loop, delayAfterMs)
        };

        try
        {
            using var client = GhostsApiClient.Create();
            using var response = await client.PostAsJsonAsync("/api/timelines", payload, JsonOptions, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return JsonSerializer.Serialize(new
            {
                ok = response.IsSuccessStatusCode,
                status = (int)response.StatusCode,
                reason = response.ReasonPhrase,
                response = TryParseJson(body),
                request = payload
            }, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            return ApiError("send_browser_timeline", ex);
        }
    }

    /// <summary>
    /// Posts a scenario document as-is and returns the API's answer. The document goes through as raw
    /// JSON rather than being deserialized and re-serialized here: a canonical document is a byte
    /// sequence its author is entitled to, and a round trip through this tool must not reorder it. A
    /// document that is not JSON is reported here instead of being sent.
    /// </summary>
    private static async Task<string> PostDocumentAsync(string path, string document, CancellationToken ct)
    {
        if (TryParseJson(document) is not JsonElement { ValueKind: JsonValueKind.Object })
        {
            return JsonSerializer.Serialize(new
            {
                ok = false,
                operation = path,
                error = "The document must be a JSON object. Nothing was sent to the API."
            }, JsonOptions);
        }

        try
        {
            using var client = GhostsApiClient.Create();
            using var content = new StringContent(document, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(path, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return JsonSerializer.Serialize(new
            {
                ok = response.IsSuccessStatusCode,
                status = (int)response.StatusCode,
                reason = response.ReasonPhrase,
                data = TryParseJson(body)
            }, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            return ApiError(path, ex);
        }
    }

    private static async Task<string> GetJsonAsync(string path, int? take, CancellationToken ct)
    {
        try
        {
            using var client = GhostsApiClient.Create();
            using var response = await client.GetAsync(path, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            return JsonSerializer.Serialize(new
            {
                ok = response.IsSuccessStatusCode,
                status = (int)response.StatusCode,
                reason = response.ReasonPhrase,
                data = Limit(TryParseJson(body), take)
            }, JsonOptions);
        }
        catch (HttpRequestException ex)
        {
            return ApiError(path, ex);
        }
    }

    private static string ApiError(string operation, HttpRequestException ex)
    {
        return JsonSerializer.Serialize(new
        {
            ok = false,
            operation,
            GhostsApiClient.BaseUrl,
            error = ex.Message
        }, JsonOptions);
    }

    private static object? TryParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static object? Limit(object? value, int? take)
    {
        if (value is not JsonElement element || take is null || element.ValueKind != JsonValueKind.Array)
            return value;

        var max = Math.Clamp(take.Value, 1, 100);
        return element.EnumerateArray().Take(max).ToArray();
    }
}

public static class GhostsApiClient
{
    public static string BaseUrl =>
        Environment.GetEnvironmentVariable("GHOSTS_API_BASE_URL")?.TrimEnd('/')
        ?? "http://localhost:5000";

    public static HttpClient Create()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl)
        };

        var token = Environment.GetEnvironmentVariable("GHOSTS_API_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}

internal static class BrowserTimelineFactory
{
    public static object Create(string url, string browser, bool loop, int delayAfterMs)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl))
            throw new ArgumentException("URL must be absolute.", nameof(url));

        var handlerType = NormalizeBrowser(browser);

        return new
        {
            id = Guid.NewGuid(),
            status = "Run",
            timeLineHandlers = new[]
            {
                new
                {
                    handlerType,
                    initial = parsedUrl.ToString(),
                    utcTimeOn = "00:00:00",
                    utcTimeOff = "23:59:59",
                    handlerArgs = new Dictionary<string, object>(),
                    loop,
                    timeLineEvents = new[]
                    {
                        new
                        {
                            command = "browse",
                            commandArgs = new object[] { parsedUrl.ToString() },
                            delayAfter = Math.Max(0, delayAfterMs),
                            delayBefore = 0
                        }
                    },
                    scheduleType = "Other"
                }
            }
        };
    }

    private static string NormalizeBrowser(string browser)
    {
        return browser.Trim().ToLowerInvariant() switch
        {
            "chrome" or "browserchrome" => "BrowserChrome",
            "firefox" or "browserfirefox" => "BrowserFirefox",
            "edge" or "browseredge" => "BrowserEdge",
            _ => throw new ArgumentException("Browser must be BrowserChrome, BrowserFirefox, or BrowserEdge.", nameof(browser))
        };
    }
}
