using System.Text;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Content;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers;

[ApiController]
[SwaggerTag("Pandora dynamic content generation endpoints")]
public class PandoraController : BaseController
{
    private readonly ApplicationConfiguration _configuration;
    private readonly IPdfGenerationService _pdfService;
    private readonly IOfficeDocumentGenerationService _officeService;
    private readonly IImageGenerationService _imageService;
    private readonly IDataFormatGenerationService _dataFormatService;
    private readonly IArchiveGenerationService _archiveService;
    private readonly IVideoGenerationService _videoService;
    private readonly IAudioGenerationService _audioService;
    private readonly IBinaryGenerationService _binaryService;
    private readonly ContentManager _contentManager;
    private readonly World _world;
    private readonly DocumentComposer _composer;

    public PandoraController(
        ILogger<PandoraController> logger,
        ApplicationConfiguration configuration,
        IPdfGenerationService pdfService,
        IOfficeDocumentGenerationService officeService,
        IImageGenerationService imageService,
        IDataFormatGenerationService dataFormatService,
        IArchiveGenerationService archiveService,
        IVideoGenerationService videoService,
        IAudioGenerationService audioService,
        IBinaryGenerationService binaryService,
        ContentManager contentManager,
        World world,
        DocumentComposer composer) : base(logger)
    {
        _configuration = configuration;
        _pdfService = pdfService;
        _officeService = officeService;
        _imageService = imageService;
        _dataFormatService = dataFormatService;
        _archiveService = archiveService;
        _videoService = videoService;
        _audioService = audioService;
        _binaryService = binaryService;
        _contentManager = contentManager;
        _world = world;
        _composer = composer;
    }

    /// <summary>
    /// Serves the content for a request path: the stored copy if there is one, else newly generated
    /// content, which is stored for next time. <paramref name="download"/> names the file with its
    /// real extension, so "memo.doc" downloads as "memo.docx", the format it actually is.
    /// </summary>
    private IActionResult Serve(string category, string path, string defaultName, string extension, string mimeType,
        bool download, Func<ContentRequest, byte[]> generate)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        var requestPath = string.IsNullOrEmpty(path) ? $"{defaultName}.{extension}" : path;
        Logger.LogTrace("{Category} request: {Path}", category, ForLog(requestPath));

        var content = _contentManager.GetOrCreate(category, requestPath, extension,
            () => generate(ContentRequest.Parse(requestPath, _world)));

        if (!download)
            return File(content, mimeType);

        var stem = Path.GetFileNameWithoutExtension(Path.GetFileName(requestPath));
        return File(content, mimeType, $"{(string.IsNullOrEmpty(stem) ? defaultName : stem)}.{extension}");
    }

    private const string Utf8 = "; charset=utf-8";

    #region PDF Endpoints

    [HttpGet("/pdf")]
    [HttpPost("/pdf")]
    [HttpGet("/pdf/{**path}")]
    [HttpPost("/pdf/{**path}")]
    [SwaggerOperation(Summary = "Generate a random PDF document", Tags = new[] { "Documents" })]
    public IActionResult GeneratePdf(string path = null)
    {
        return Serve("pdf", path, "index", "pdf", "application/pdf", true,
            r => _pdfService.GeneratePdf(_composer.Compose(r)));
    }

    #endregion

    #region Office Document Endpoints

    [HttpGet("/doc")]
    [HttpPost("/doc")]
    [HttpGet("/docx")]
    [HttpPost("/docx")]
    [HttpGet("/docs")]
    [HttpPost("/docs")]
    [HttpGet("/documents")]
    [HttpPost("/documents")]
    [HttpGet("/doc/{**path}")]
    [HttpPost("/doc/{**path}")]
    [HttpGet("/docx/{**path}")]
    [HttpPost("/docx/{**path}")]
    [HttpGet("/docs/{**path}")]
    [HttpPost("/docs/{**path}")]
    [HttpGet("/documents/{**path}")]
    [HttpPost("/documents/{**path}")]
    [SwaggerOperation(Summary = "Generate a random Word document", Tags = new[] { "Documents" })]
    public IActionResult GenerateWordDocument(string path = null)
    {
        return Serve("docx", path, "index", "docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", true,
            r => _officeService.GenerateWordDocument(_composer.Compose(r)));
    }

    [HttpGet("/xlsx")]
    [HttpPost("/xlsx")]
    [HttpGet("/sheets")]
    [HttpPost("/sheets")]
    [HttpGet("/xlsx/{**path}")]
    [HttpPost("/xlsx/{**path}")]
    [HttpGet("/sheets/{**path}")]
    [HttpPost("/sheets/{**path}")]
    [SwaggerOperation(Summary = "Generate a random Excel spreadsheet", Tags = new[] { "Spreadsheets" })]
    public IActionResult GenerateExcelDocument(string path = null)
    {
        return Serve("xlsx", path, "index", "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", true,
            r => _officeService.GenerateExcelDocument(_composer.Compose(r), _composer.ComposeTable(r)));
    }

    [HttpGet("/ppt")]
    [HttpPost("/ppt")]
    [HttpGet("/pptx")]
    [HttpPost("/pptx")]
    [HttpGet("/slides")]
    [HttpPost("/slides")]
    [HttpGet("/ppt/{**path}")]
    [HttpPost("/ppt/{**path}")]
    [HttpGet("/pptx/{**path}")]
    [HttpPost("/pptx/{**path}")]
    [HttpGet("/slides/{**path}")]
    [HttpPost("/slides/{**path}")]
    [SwaggerOperation(Summary = "Generate a random PowerPoint presentation", Tags = new[] { "Presentations" })]
    public IActionResult GeneratePowerPointDocument(string path = null)
    {
        return Serve("pptx", path, "index", "pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", true,
            r => _officeService.GeneratePowerPointDocument(_composer.Compose(r)));
    }

    #endregion

    #region Image Endpoints

    [HttpGet("/i")]
    [HttpPost("/i")]
    [HttpGet("/img")]
    [HttpPost("/img")]
    [HttpGet("/images")]
    [HttpPost("/images")]
    [HttpGet("/i/{**path}")]
    [HttpPost("/i/{**path}")]
    [HttpGet("/img/{**path}")]
    [HttpPost("/img/{**path}")]
    [HttpGet("/images/{**path}")]
    [HttpPost("/images/{**path}")]
    [SwaggerOperation(Summary = "Generate a random image", Tags = new[] { "Image" })]
    public IActionResult GenerateImage(string path = null)
    {
        var format = "png";
        if (!string.IsNullOrEmpty(path))
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLower();
            if (new[] { "jpg", "jpeg", "png", "gif" }.Contains(ext))
            {
                format = ext;
            }
        }

        var mimeType = format switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            _ => "image/png"
        };

        // Return without filename to display inline instead of forcing download
        return Serve("img", path, "index", format, mimeType, false, _ => _imageService.GenerateImage(format));
    }

    #endregion

    #region Data Format Endpoints

    [HttpGet("/api")]
    [HttpPost("/api")]
    [HttpGet("/json")]
    [HttpPost("/json")]
    [HttpGet("/api/{**path}")]
    [HttpPost("/api/{**path}")]
    [HttpGet("/json/{**path}")]
    [HttpPost("/json/{**path}")]
    [SwaggerOperation(Summary = "Generate random JSON data", Tags = new[] { "Data Structures" })]
    public IActionResult GenerateJson(string path = null)
    {
        return Serve("json", path, "index", "json", "application/json", false,
            r => Encoding.UTF8.GetBytes(_dataFormatService.GenerateJson(_composer.Compose(r), _composer.ComposeTable(r))));
    }

    [HttpGet("/csv")]
    [HttpPost("/csv")]
    [HttpGet("/csv/{**path}")]
    [HttpPost("/csv/{**path}")]
    [SwaggerOperation(Summary = "Generate random CSV data", Tags = new[] { "Data Structures" })]
    public IActionResult GenerateCsv(string path = null)
    {
        return Serve("csv", path, "index", "csv", "text/csv", true,
            r => _dataFormatService.GenerateCsv(_composer.ComposeTable(r)));
    }

    [HttpGet("/text")]
    [HttpPost("/text")]
    [HttpGet("/txt")]
    [HttpPost("/txt")]
    [HttpGet("/text/{**path}")]
    [HttpPost("/text/{**path}")]
    [HttpGet("/txt/{**path}")]
    [HttpPost("/txt/{**path}")]
    [SwaggerOperation(Summary = "Generate random text", Tags = new[] { "Data Structures" })]
    public IActionResult GenerateText(string path = null)
    {
        return Serve("txt", path, "index", "txt", "text/plain" + Utf8, false,
            r => Encoding.UTF8.GetBytes(_dataFormatService.GenerateText(_composer.Compose(r))));
    }

    [HttpGet("/html")]
    [HttpPost("/html")]
    [HttpGet("/html/{**path}")]
    [HttpPost("/html/{**path}")]
    [SwaggerOperation(Summary = "Generate random HTML", Tags = new[] { "Web" })]
    public IActionResult GenerateHtml(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        // If in social mode, return the themed Index view
        if (_configuration.Mode.Type.Equals("social", StringComparison.OrdinalIgnoreCase))
        {
            // Redirect to root to show the proper themed social feed
            return RedirectToAction("Index", "Posts");
        }

        // Website mode - return standalone HTML
        return Serve("html", path, "index", "html", "text/html" + Utf8, false,
            _ => Encoding.UTF8.GetBytes(_dataFormatService.GenerateHtml()));
    }

    [HttpGet("/js")]
    [HttpPost("/js")]
    [HttpGet("/script")]
    [HttpPost("/script")]
    [HttpGet("/js/{**path}")]
    [HttpPost("/js/{**path}")]
    [HttpGet("/script/{**path}")]
    [HttpPost("/script/{**path}")]
    [SwaggerOperation(Summary = "Generate random JavaScript", Tags = new[] { "Web" })]
    public IActionResult GenerateScript(string path = null)
    {
        return Serve("js", path, "script", "js", "application/javascript" + Utf8, false,
            _ => Encoding.UTF8.GetBytes(_dataFormatService.GenerateScript()));
    }

    [HttpGet("/css")]
    [HttpPost("/css")]
    [HttpGet("/stylesheet")]
    [HttpPost("/stylesheet")]
    [HttpGet("/css/{**path}")]
    [HttpPost("/css/{**path}")]
    [HttpGet("/stylesheet/{**path}")]
    [HttpPost("/stylesheet/{**path}")]
    [SwaggerOperation(Summary = "Generate random CSS", Tags = new[] { "Web" })]
    public IActionResult GenerateStylesheet(string path = null)
    {
        return Serve("css", path, "styles", "css", "text/css" + Utf8, false,
            _ => Encoding.UTF8.GetBytes(_dataFormatService.GenerateStylesheet()));
    }

    #endregion

    #region Archive Endpoints

    [HttpGet("/zip")]
    [HttpPost("/zip")]
    [HttpGet("/zip/{**path}")]
    [HttpPost("/zip/{**path}")]
    [SwaggerOperation(Summary = "Generate a random ZIP archive", Tags = new[] { "Archives" })]
    public IActionResult GenerateZip(string path = null)
    {
        return Serve("zip", path, "archive", "zip", "application/zip", true, _ => _archiveService.GenerateZip());
    }

    [HttpGet("/tar")]
    [HttpPost("/tar")]
    [HttpGet("/tar/{**path}")]
    [HttpPost("/tar/{**path}")]
    [SwaggerOperation(Summary = "Generate a random TAR archive", Tags = new[] { "Archives" })]
    public IActionResult GenerateTar(string path = null)
    {
        return Serve("tar", path, "archive", "tar", "application/x-tar", true, _ => _archiveService.GenerateTar());
    }

    #endregion

    #region Video Endpoints

    [HttpGet("/video")]
    [HttpPost("/video")]
    [HttpGet("/videos")]
    [HttpPost("/videos")]
    [HttpGet("/video/{**path}")]
    [HttpPost("/video/{**path}")]
    [HttpGet("/videos/{**path}")]
    [HttpPost("/videos/{**path}")]
    [SwaggerOperation(Summary = "Generate a random video file", Tags = new[] { "Video" })]
    public IActionResult GenerateVideo(string path = null)
    {
        var format = "mp4";
        if (!string.IsNullOrEmpty(path))
        {
            // map onto a literal, so no part of the request path reaches the generator
            format = Path.GetExtension(path).TrimStart('.').ToLower() switch
            {
                "avi" => "avi",
                "mov" => "mov",
                "mkv" => "mkv",
                "webm" => "webm",
                _ => "mp4"
            };
        }

        // Return without filename to allow inline playback
        return Serve("video", path, "video", format, "video/mp4", false, _ => _videoService.GenerateVideo(format));
    }

    #endregion

    #region Audio Endpoints

    [HttpGet("/audio")]
    [HttpPost("/audio")]
    [HttpGet("/voice")]
    [HttpPost("/voice")]
    [HttpGet("/call")]
    [HttpPost("/call")]
    [HttpGet("/calls")]
    [HttpPost("/calls")]
    [HttpGet("/audio/{**path}")]
    [HttpPost("/audio/{**path}")]
    [HttpGet("/voice/{**path}")]
    [HttpPost("/voice/{**path}")]
    [HttpGet("/call/{**path}")]
    [HttpPost("/call/{**path}")]
    [HttpGet("/calls/{**path}")]
    [HttpPost("/calls/{**path}")]
    [SwaggerOperation(Summary = "Generate a random audio file", Tags = new[] { "Audio" })]
    public IActionResult GenerateAudio(string path = null)
    {
        var format = "wav";
        if (!string.IsNullOrEmpty(path))
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLower();
            if (new[] { "wav", "mp3", "ogg", "m4a" }.Contains(ext))
            {
                format = ext;
            }
        }

        var mimeType = format switch
        {
            "wav" => "audio/wav",
            "mp3" => "audio/mpeg",
            "ogg" => "audio/ogg",
            "m4a" => "audio/mp4",
            _ => "audio/wav"
        };

        // Return without filename to allow inline playback
        return Serve("audio", path, "audio", format, mimeType, false, _ => _audioService.GenerateAudio(format));
    }

    #endregion

    #region Binary File Endpoints

    [HttpGet("/bin")]
    [HttpPost("/bin")]
    [HttpGet("/binary")]
    [HttpPost("/binary")]
    [HttpGet("/binaries")]
    [HttpPost("/binaries")]
    [HttpGet("/bin/{**path}")]
    [HttpPost("/bin/{**path}")]
    [HttpGet("/binary/{**path}")]
    [HttpPost("/binary/{**path}")]
    [HttpGet("/binaries/{**path}")]
    [HttpPost("/binaries/{**path}")]
    [SwaggerOperation(Summary = "Generate a random binary file", Tags = new[] { "Binary" })]
    public IActionResult GenerateBinary(string path = null)
    {
        return Serve("bin", path, "file", "bin", "application/octet-stream", true, _ => _binaryService.GenerateBinary());
    }

    [HttpGet("/onenote")]
    [HttpPost("/onenote")]
    [HttpGet("/onenote/{**path}")]
    [HttpPost("/onenote/{**path}")]
    [SwaggerOperation(Summary = "Generate a random OneNote file", Tags = new[] { "Documents" })]
    public IActionResult GenerateOneNote(string path = null)
    {
        return Serve("one", path, "notebook", "one", "application/onenote", true, _ => _binaryService.GenerateOneNote());
    }

    [HttpGet("/exe")]
    [HttpPost("/exe")]
    [HttpGet("/exe/{**path}")]
    [HttpPost("/exe/{**path}")]
    [SwaggerOperation(Summary = "Generate a fake executable file", Tags = new[] { "Executables" })]
    public IActionResult GenerateExe(string path = null)
    {
        return Serve("exe", path, "program", "exe", "application/octet-stream", true, _ => _binaryService.GenerateExecutable("exe"));
    }

    [HttpGet("/msi")]
    [HttpPost("/msi")]
    [HttpGet("/msi/{**path}")]
    [HttpPost("/msi/{**path}")]
    [SwaggerOperation(Summary = "Generate a fake MSI installer file", Tags = new[] { "Executables" })]
    public IActionResult GenerateMsi(string path = null)
    {
        return Serve("msi", path, "installer", "msi", "application/x-msdownload", true, _ => _binaryService.GenerateExecutable("msi"));
    }

    [HttpGet("/iso")]
    [HttpPost("/iso")]
    [HttpGet("/iso/{**path}")]
    [HttpPost("/iso/{**path}")]
    [SwaggerOperation(Summary = "Generate a fake ISO disk image", Tags = new[] { "Disk Images" })]
    public IActionResult GenerateIso(string path = null)
    {
        return Serve("iso", path, "disk", "iso", "application/octet-stream", true, _ => _binaryService.GenerateIso());
    }

    #endregion

    #region Info Endpoint

    [HttpGet("/pandora/about")]
    [SwaggerOperation(Summary = "Get information about Pandora", Tags = new[] { "Information" })]
    public IActionResult About()
    {
        return Ok(new
        {
            version = "1.0.0",
            enabled = _configuration.Pandora.Enabled,
            message = "GHOSTS PANDORA CONTENT SERVER",
            copyright = "Carnegie Mellon University. All Rights Reserved.",
            features = new
            {
                documents = new[] { "PDF", "Word", "Excel", "PowerPoint", "OneNote" },
                data = new[] { "JSON", "CSV", "Text", "HTML" },
                web = new[] { "JavaScript", "CSS" },
                images = new[] { "PNG", "JPEG", "GIF" },
                archives = new[] { "ZIP", "TAR" },
                video = new[] { "MP4", "AVI", "MOV", "MKV", "WEBM" },
                audio = new[] { "WAV", "MP3", "OGG", "M4A" },
                binary = new[] { "Binary", "Executable (EXE)", "Installer (MSI)", "ISO" }
            }
        });
    }

    #endregion
}
