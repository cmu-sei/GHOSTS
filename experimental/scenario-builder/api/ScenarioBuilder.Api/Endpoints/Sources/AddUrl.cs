// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using ScenarioBuilder.Api.Domain;
using ScenarioBuilder.Api.Infrastructure;

namespace ScenarioBuilder.Api.Endpoints.Sources;

public record AddUrlSource(string? Url, string? Name);

/// <summary>
/// POST /scenarios/{id}/sources/url — a page's text, fetched once now by the API. A PDF link is read as a PDF.
/// On a closed range most links won't resolve; the refusal says so and asks for the text instead.
/// </summary>
public class AddUrlEndpoint : IEndpoint
{
    public RouteHandlerBuilder Register(RouteGroupBuilder group, string tag) =>
        group.MapPost("/scenarios/{id:int}/sources/url",
                async (int id, AddUrlSource body, HttpContext http, IHttpClientFactory clients, BuilderDb db, CancellationToken ct) =>
                {
                    if (await db.Scenarios.FindAsync([id], ct) is not { } scenario)
                        return ScenarioState.Missing(id);
                    var caller = Caller.From(http);
                    if (SourceRules.AgentRefused(caller) is { } refused)
                        return refused;
                    if (!Uri.TryCreate(body.Url?.Trim(), UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
                        return ErrorResults.BadRequest("URL_INVALID", "Give a full web address, starting with http:// or https://.");

                    string text;
                    string? title;
                    try
                    {
                        (text, title) = await Fetch(clients.CreateClient(SourceText.WebClient), url, ct);
                    }
                    catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
                    {
                        var reason = e is TaskCanceledException ? "it took longer than 20 seconds" : e.Message;
                        return ErrorResults.Unprocessable("URL_UNREACHABLE", $"Couldn't fetch {url}: {reason}. If it can't be reached from here, paste its text instead.");
                    }
                    var name = !string.IsNullOrWhiteSpace(body.Name) ? body.Name.Trim() : title ?? (url.Host + url.AbsolutePath).TrimEnd('/');
                    return await SourceRules.Save(db, scenario, caller, SourceKind.Url, name, url.ToString(), text, ct);
                })
            .WithTags(tag);

    private static async Task<(string Text, string? Title)> Fetch(HttpClient client, Uri url, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"the site answered HTTP {(int)response.StatusCode}");

        using var body = new MemoryStream();
        await using (var stream = await response.Content.ReadAsStreamAsync(ct))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                body.Write(buffer, 0, read);
                if (body.Length > SourceText.MaxPageBytes)
                    throw new InvalidOperationException($"the page is over {SourceText.MaxPageBytes / 1024 / 1024} MB");
            }
        }

        var media = response.Content.Headers.ContentType?.MediaType ?? "";
        var data = body.ToArray();
        if (media == "application/pdf" || url.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return (SourceText.Pdf(data), null);
        if (media.StartsWith("text/plain") || media.Contains("json") || media == "text/csv" || media == "text/markdown")
            return (SourceText.Decode(data), null);
        return SourceText.FromHtml(SourceText.Decode(data));
    }
}
