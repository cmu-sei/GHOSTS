using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/images")]
[Route("/files")]
public class FilesController(ILogger logger) : BaseController(logger)
{
    /// <summary>
    /// General file upload. Content is stored outside wwwroot and is only ever handed
    /// back as an attachment by <see cref="DownloadFile"/>, so arbitrary file types are
    /// accepted without being served inline from the application origin.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(FileInputModel.MaxFileBytes)]
    public async Task<IActionResult> UploadFile([FromForm] FileInputModel model)
    {
        Logger.LogTrace("{RequestScheme}://{RequestHost}{RequestPath}{RequestQueryString}|{RequestMethod}|{Join}", Request.Scheme, Request.Host, Request.Path, Request.QueryString, Request.Method, string.Join(",", Request.Form));

        try
        {
            return Ok($"/files/{await model.SaveFileAsync()}");
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
    }

    [HttpGet("/files/{id:guid}")]
    public IActionResult DownloadFile(Guid id)
    {
        var uploadDir = Path.Combine(FileInputModel.UploadsRoot, id.ToString());
        var contentPath = Path.Combine(uploadDir, FileInputModel.ContentFileName);
        if (!System.IO.File.Exists(contentPath))
            return NotFound();

        var namePath = Path.Combine(uploadDir, FileInputModel.NameFileName);
        var downloadName = System.IO.File.Exists(namePath)
            ? System.IO.File.ReadAllText(namePath)
            : "download";

        // Never let a browser sniff its way to rendering this inline
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(contentPath, "application/octet-stream", downloadName);
    }

    public class FileInputModel
    {
        [Required] public IFormFile File { get; set; }

        private const long MaxImageBytes = 8 * 1024 * 1024;

        // Also applied as a RequestSizeLimit on the upload action, so an oversized body is
        // refused before it is buffered rather than after
        internal const long MaxFileBytes = 32 * 1024 * 1024;

        private const long MaxTotalBytes = 512L * 1024 * 1024;
        private const long MaxPixels = 50_000_000;
        private const int MaxNameLength = 128;

        internal const string ContentFileName = "content";
        internal const string NameFileName = "name";

        internal static string UploadsRoot => Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        internal static string ImagesRoot => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

        // Quota covers both upload roots, and is held across the check and the write so
        // concurrent uploads cannot each observe the same free space and all proceed
        private static readonly SemaphoreSlim StorageLock = new(1, 1);

        /// <summary>
        /// Saves an image that will be displayed inline in a post, under
        /// wwwroot/images/{guid}/, and returns its served path. The image is identified by
        /// decoding it rather than by trusting the supplied filename, extension or content
        /// type, and is re-encoded before being written, so anything Skia cannot decode
        /// (HTML, SVG, scripts) is rejected. The stored name is generated, so no part of
        /// the save path comes from the caller.
        /// </summary>
        public async Task<string> SaveImageAsync()
        {
            if (File.Length is 0 or > MaxImageBytes)
                throw new InvalidOperationException("Image must not be empty or larger than 8MB.");

            using var upload = new MemoryStream();
            await File.CopyToAsync(upload);
            upload.Position = 0;

            using var codec = SKCodec.Create(upload)
                              ?? throw new InvalidOperationException("Upload is not a supported image.");

            // Guard against a small file that decodes into an enormous bitmap
            if ((long)codec.Info.Width * codec.Info.Height > MaxPixels)
                throw new InvalidOperationException("Image dimensions are too large.");

            using var bitmap = SKBitmap.Decode(codec)
                               ?? throw new InvalidOperationException("Upload is not a supported image.");

            var format = bitmap.Info.IsOpaque ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
            var storedName = bitmap.Info.IsOpaque ? "image.jpg" : "image.png";

            var guid = Guid.NewGuid().ToString();
            var uploadDir = Path.Combine(ImagesRoot, guid);

            using var encoded = bitmap.Encode(format, 90);

            await StorageLock.WaitAsync();
            try
            {
                if (UsageBytes() + encoded.Size > MaxTotalBytes)
                    throw new InvalidOperationException("Upload storage quota exceeded.");

                Directory.CreateDirectory(uploadDir);
                await using var output = new FileStream(Path.Combine(uploadDir, storedName), FileMode.Create);
                encoded.SaveTo(output);
            }
            finally
            {
                StorageLock.Release();
            }

            return $"/images/{guid}/{storedName}";
        }

        /// <summary>
        /// Stores a general upload of any type under uploads/{guid}/, outside wwwroot and
        /// outside every Razor view location, using generated storage names. The supplied
        /// filename is kept only as download metadata and never forms part of a save path.
        /// </summary>
        public async Task<Guid> SaveFileAsync()
        {
            if (File.Length is 0 or > MaxFileBytes)
                throw new InvalidOperationException("Upload must not be empty or larger than 32MB.");

            var id = Guid.NewGuid();
            var uploadDir = Path.Combine(UploadsRoot, id.ToString());

            await StorageLock.WaitAsync();
            try
            {
                if (UsageBytes() + File.Length > MaxTotalBytes)
                    throw new InvalidOperationException("Upload storage quota exceeded.");

                Directory.CreateDirectory(uploadDir);
                await using var output = new FileStream(Path.Combine(uploadDir, ContentFileName), FileMode.Create);
                await File.CopyToAsync(output);
            }
            finally
            {
                StorageLock.Release();
            }

            var originalName = Path.GetFileName(File.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
                originalName = "download";
            if (originalName.Length > MaxNameLength)
                originalName = originalName[..MaxNameLength];

            await System.IO.File.WriteAllTextAsync(Path.Combine(uploadDir, NameFileName), originalName);

            return id;
        }

        private static long UsageBytes() => RootUsageBytes(UploadsRoot) + RootUsageBytes(ImagesRoot);

        private static long RootUsageBytes(string root) =>
            Directory.Exists(root)
                ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                    .Sum(f => new FileInfo(f).Length)
                : 0;
    }
}
