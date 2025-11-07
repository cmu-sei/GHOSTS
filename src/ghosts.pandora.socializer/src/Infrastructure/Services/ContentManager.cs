using System.Web;

namespace Ghosts.Socializer.Infrastructure.Services;

public class ContentManager
{
    private readonly ApplicationConfiguration _configuration;
    private readonly ILogger<ContentManager> _logger;
    private readonly string _baseDir;
    private readonly string _defaultFileName;
    private readonly string _extension;

    public string FileName { get; private set; }
    public string RelativePath { get; private set; }
    public string FullPath { get; private set; }

    public ContentManager(
        ApplicationConfiguration configuration,
        ILogger<ContentManager> logger,
        string defaultFileName = "index",
        string extension = "txt")
    {
        _configuration = configuration;
        _logger = logger;
        _baseDir = configuration.Pandora.ContentCacheDirectory;
        _defaultFileName = defaultFileName;
        _extension = extension;
    }

    public bool IsStoring()
    {
        return _configuration.Pandora.StoreResults;
    }

    public void Resolve(HttpRequest request)
    {
        var urlPath = HttpUtility.UrlDecode(request.Path.Value?.TrimStart('/') ?? "");

        if (urlPath.EndsWith("/"))
        {
            urlPath += $"{_defaultFileName}.{_extension}";
        }
        else if (!urlPath.EndsWith($".{_extension}"))
        {
            urlPath += $".{_extension}";
        }

        RelativePath = urlPath;
        FileName = Path.GetFileName(urlPath);
        FullPath = Path.Combine(_baseDir, urlPath);

        var directory = Path.GetDirectoryName(FullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _logger.LogInformation("{Extension} request received. Filename: {FileName} Path: {RelativePath}",
            _extension.ToUpper(), FileName, RelativePath);
    }

    public string GuessMediaType()
    {
        var extension = Path.GetExtension(FullPath);
        return extension.ToLower() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            _ => "application/octet-stream"
        };
    }

    public async Task<byte[]> LoadAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(FullPath) || !File.Exists(FullPath))
            {
                return null;
            }

            return await File.ReadAllBytesAsync(FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cached content from {FullPath}", FullPath);
            return null;
        }
    }

    public async Task SaveAsync(byte[] content)
    {
        try
        {
            var directory = Path.GetDirectoryName(FullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(FullPath, content);
            _logger.LogDebug("Saved content to {FullPath}", FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save content to {FullPath}", FullPath);
        }
    }

    public async Task SaveAsync(string content)
    {
        try
        {
            var directory = Path.GetDirectoryName(FullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(FullPath, content);
            _logger.LogDebug("Saved content to {FullPath}", FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save content to {FullPath}", FullPath);
        }
    }
}
