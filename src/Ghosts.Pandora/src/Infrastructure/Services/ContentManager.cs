namespace Ghosts.Pandora.Infrastructure.Services;

/// <summary>
/// Keeps generated content on disk, so a URL returns the same file every time it is asked for.
/// Content is generated once per path and kept until the content directory is deleted; nothing
/// here expires or rewrites it. When <c>StoreResults</c> is off, or the store is full, content
/// is still generated (deterministically) but not written.
/// </summary>
public class ContentManager
{
    private readonly ApplicationConfiguration _configuration;
    private readonly ILogger<ContentManager> _logger;
    private readonly long _maxBytes;
    private long _storedBytes;
    private bool _fullLogged;

    // one lock per stripe of keys, so two requests for the same path generate it once
    private readonly object[] _locks = Enumerable.Range(0, 64).Select(_ => new object()).ToArray();

    public string Root { get; }

    public ContentManager(ApplicationConfiguration configuration, IWebHostEnvironment environment, ILogger<ContentManager> logger)
    {
        _configuration = configuration;
        _logger = logger;
        Root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuration.Pandora.ContentCacheDirectory));
        _maxBytes = configuration.Pandora.MaxStoredMegabytes * 1024L * 1024L;

        if (Directory.Exists(Root))
            _storedBytes = Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
    }

    public bool IsStoring() => _configuration.Pandora.StoreResults;

    /// <summary>
    /// Returns the stored content for <paramref name="path"/> under <paramref name="category"/>,
    /// or generates, stores and returns it.
    /// </summary>
    public byte[] GetOrCreate(string category, string path, string extension, Func<byte[]> generate)
    {
        var relativePath = SafeRelativePath(category, path, extension);
        if (!IsStoring() || relativePath == null)
        {
            if (relativePath == null)
                _logger.LogDebug("Not storing content for an unsafe path in {Category}", category);
            return generate();
        }

        var fullPath = Path.GetFullPath(Path.Combine(Root, relativePath));
        if (!fullPath.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return generate();

        lock (_locks[(int)(Content.ContentRequest.StableHash(relativePath) % (uint)_locks.Length)])
        {
            var stored = Load(fullPath);
            if (stored != null)
                return stored;

            var content = generate();
            Save(fullPath, content);
            return content;
        }
    }

    /// <summary>
    /// The store path for a request, or null when the path cannot be stored safely. Every
    /// segment must be a plain file name: no "." or "..", no separators, no control characters.
    /// </summary>
    public static string SafeRelativePath(string category, string path, string extension)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (segments.Count == 0)
            segments.Add("index");
        if (segments.Count > 16)
            return null;

        var invalid = Path.GetInvalidFileNameChars();
        foreach (var segment in segments)
        {
            if (segment is "." or ".." || segment.Length > 100 || segment.Trim() != segment ||
                segment.IndexOfAny(invalid) >= 0 || segment.Contains('\\') || segment.Contains(':') || segment.Any(char.IsControl))
                return null;
        }

        // a stored file always carries the extension it is served with: memo.doc is kept as memo.docx
        var last = segments[^1];
        var stem = Path.GetFileNameWithoutExtension(last);
        segments[^1] = $"{(string.IsNullOrEmpty(stem) ? "index" : stem)}.{extension}";

        return Path.Combine([category, .. segments]);
    }

    private byte[] Load(string fullPath)
    {
        try
        {
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Could not read stored content {FullPath}: {Message}", fullPath, e.Message);
            return null;
        }
    }

    /// <summary>Writes to a temporary file and moves it into place, so a reader never sees a partial file.</summary>
    private void Save(string fullPath, byte[] content)
    {
        if (Interlocked.Read(ref _storedBytes) + content.Length > _maxBytes)
        {
            if (!_fullLogged)
            {
                _logger.LogWarning("Content store {Root} has reached its {Megabytes} MB limit; new content is served but not stored",
                    Root, _configuration.Pandora.MaxStoredMegabytes);
                _fullLogged = true;
            }
            return;
        }

        var temporary = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, fullPath, overwrite: false);
            Interlocked.Add(ref _storedBytes, content.Length);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // another process may have stored the same path first; its copy wins
            _logger.LogWarning("Could not store content {FullPath}: {Message}", fullPath, e.Message);
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug("Could not remove {Temporary}: {Message}", temporary, cleanup.Message);
            }
        }
    }
}
