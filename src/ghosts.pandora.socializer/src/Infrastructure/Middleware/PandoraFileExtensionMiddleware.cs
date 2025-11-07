namespace Ghosts.Socializer.Infrastructure.Middleware;

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

    public PandoraFileExtensionMiddleware(RequestDelegate next, ILogger<PandoraFileExtensionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationConfiguration configuration)
    {
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

        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path))
        {
            await _next(context);
            return;
        }

        // Check if the path ends with one of the supported extensions
        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (ExtensionToRoute.TryGetValue(extension, out var route))
        {
            // Skip if the path already starts with a known pandora or socializer route
            var skipPrefixes = new[] { "/pdf", "/doc", "/docx", "/xlsx", "/ppt", "/img", "/i", "/images",
                                       "/json", "/api", "/csv", "/text", "/txt", "/html", "/js", "/css",
                                       "/zip", "/tar", "/sheets", "/slides", "/script", "/stylesheet",
                                       "/video", "/videos", "/audio", "/voice", "/call", "/calls",
                                       "/bin", "/binary", "/binaries", "/onenote", "/exe", "/msi", "/iso",
                                       "/pandora", "/files", "/posts", "/users", "/auth",
                                       "/search", "/hubs", "/swagger", "/wwwroot" };

            if (skipPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
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
