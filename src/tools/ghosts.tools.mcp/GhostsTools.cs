using System.ComponentModel;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    [Description("Returns the configured GHOSTS API base URL used by this MCP server.")]
    public static string GetGhostsApiBaseUrl()
    {
        return GhostsApiClient.BaseUrl;
    }

    [McpServerTool(ReadOnly = true, OpenWorld = true)]
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

    [McpServerTool(Name = "ListMachinesAsync", ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns the actual current list of GHOSTS machines now. Use this when the user asks to list, show, count, or inspect machines; return the tool result, not example code.")]
    public static async Task<string> ListMachinesAsync(
        [Description("Maximum number of machines to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync("/api/machines/list", take, ct);
    }

    [McpServerTool(ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns one GHOSTS machine record now, including recent history when available.")]
    public static async Task<string> GetMachineAsync(
        [Description("GHOSTS machine GUID.")] Guid machineId,
        CancellationToken ct = default)
    {
        return await GetJsonAsync($"/api/machines/{machineId}", null, ct);
    }

    [McpServerTool(ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns the current list of generated GHOSTS NPC IDs and names now.")]
    public static async Task<string> ListNpcsAsync(
        [Description("Maximum number of NPCs to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync("/api/npcs/list", take, ct);
    }

    [McpServerTool(ReadOnly = true, OpenWorld = true)]
    [Description("Fetches and returns the current list of GHOSTS scenarios now.")]
    public static async Task<string> ListScenariosAsync(
        [Description("Maximum number of scenarios to return.")] int take = 25,
        CancellationToken ct = default)
    {
        return await GetJsonAsync("/api/scenarios", take, ct);
    }

    [McpServerTool(ReadOnly = true, OpenWorld = false)]
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

    [McpServerTool(Destructive = false, Idempotent = false, OpenWorld = true)]
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
