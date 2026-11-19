namespace Ghosts.Pandora.Infrastructure.Middleware;

/// <summary>
/// Middleware that redirects requests with specific file extensions to the appropriate Pandora controller endpoints.
/// This enables pandora-style behavior where any URL ending in .pdf, .docx, etc. will generate that file type.
/// </summary>
public class PandoraFileExtensionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PandoraFileExtensionMiddleware> _logger;

    private static readonly Dictionary<string, string> ExtensionToRoute = new()
    {
        // Documents
        { ".pdf", "/pdf/" },
        { ".doc", "/doc/" },
        { ".docx", "/doc/" },
        { ".xls", "/xlsx/" },
        { ".xlsx", "/xlsx/" },
        { ".ppt", "/ppt/" },
        { ".pptx", "/ppt/" },
        { ".one", "/onenote/" },
        // Images
        { ".jpg", "/img/" },
        { ".jpeg", "/img/" },
        { ".png", "/img/" },
        { ".gif", "/img/" },
        // Data formats
        { ".json", "/json/" },
        { ".csv", "/csv/" },
        { ".txt", "/text/" },
        { ".html", "/html/" },
        { ".htm", "/html/" },
        // Web
        { ".js", "/js/" },
        { ".css", "/css/" },
        // Archives
        { ".zip", "/zip/" },
        { ".tar", "/tar/" },
        // Video
        { ".mp4", "/video/" },
        { ".avi", "/video/" },
        { ".mov", "/video/" },
        { ".mkv", "/video/" },
        { ".webm", "/video/" },
        // Audio
        { ".wav", "/audio/" },
        { ".mp3", "/audio/" },
        { ".ogg", "/audio/" },
        { ".m4a", "/audio/" },
        // Binary/Executables
        { ".bin", "/bin/" },
        { ".exe", "/exe/" },
        { ".msi", "/msi/" },
        { ".iso", "/iso/" }
    };

    // The first path segments each generation route also answers to, so "/docs/memo.docx" is
    // already a Word request, while "/docs/report.pdf" still asks for a PDF
    private static readonly Dictionary<string, string[]> RouteAliases = new()
    {
        ["/pdf/"] = ["pdf"],
        ["/doc/"] = ["doc", "docx", "docs", "documents"],
        ["/xlsx/"] = ["xlsx", "sheets"],
        ["/ppt/"] = ["ppt", "pptx", "slides"],
        ["/onenote/"] = ["onenote"],
        ["/img/"] = ["i", "img", "images"],
        ["/json/"] = ["json"],
        ["/csv/"] = ["csv"],
        ["/text/"] = ["text", "txt"],
        ["/html/"] = ["html"],
        ["/js/"] = ["js", "script"],
        ["/css/"] = ["css", "stylesheet"],
        ["/zip/"] = ["zip"],
        ["/tar/"] = ["tar"],
        ["/video/"] = ["video", "videos"],
        ["/audio/"] = ["audio", "voice", "call", "calls"],
        ["/bin/"] = ["bin", "binary", "binaries"],
        ["/exe/"] = ["exe"],
        ["/msi/"] = ["msi"],
        ["/iso/"] = ["iso"]
    };

    // Application routes whose paths are never rewritten, whatever they end in. /files is not one:
    // it only serves upload ids, so /files/data.xlsx is a generated spreadsheet.
    private static readonly HashSet<string> ReservedSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "hubs", "swagger", "payloads", "wwwroot",
        "posts", "post", "r", "u", "user", "users", "profile", "messages", "login", "search", "law-enforcement"
    };

    public PandoraFileExtensionMiddleware(RequestDelegate next, ILogger<PandoraFileExtensionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationConfiguration configuration)
    {
        var path = context.Request.Path.Value;

        // A configured payload answers at its own URL, ahead of anything generated there
        if (configuration.Payloads.Enabled && !string.IsNullOrEmpty(path) &&
            !path.StartsWith("/payloads/", StringComparison.OrdinalIgnoreCase) &&
            configuration.Payloads.Mappings.Any(m => !string.IsNullOrEmpty(m.Url) && path.StartsWith(m.Url, StringComparison.OrdinalIgnoreCase)))
        {
            context.Request.Path = new PathString("/payloads" + path);
            await _next(context);
            return;
        }

        if (!configuration.Pandora.Enabled)
        {
            await _next(context);
            return;
        }

        // Only rewrite GET requests - POST requests should be explicit
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrEmpty(path))
        {
            await _next(context);
            return;
        }

        // Check if the path ends with one of the supported extensions
        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (ExtensionToRoute.TryGetValue(extension, out var route))
        {
            var firstSegment = path.TrimStart('/').Split('/')[0];
            if (ReservedSegments.Contains(firstSegment) ||
                RouteAliases[route].Contains(firstSegment, StringComparer.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            _logger.LogDebug("Rewriting path from {OriginalPath} to {NewPath}", path, route + path.TrimStart('/'));

            // Rewrite the path to the appropriate pandora endpoint
            context.Request.Path = new PathString(route + path.TrimStart('/'));
        }

        await _next(context);
    }
}

public static class PandoraFileExtensionMiddlewareExtensions
{
    public static IApplicationBuilder UsePandoraFileExtensionMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PandoraFileExtensionMiddleware>();
    }
}
