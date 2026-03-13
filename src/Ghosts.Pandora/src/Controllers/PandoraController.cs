using Ghosts.Pandora.Infrastructure;
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
        IBinaryGenerationService binaryService) : base(logger)
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
    }

    #region PDF Endpoints

    [HttpGet("/pdf")]
    [HttpPost("/pdf")]
    [HttpGet("/pdf/{**path}")]
    [HttpPost("/pdf/{**path}")]
    [SwaggerOperation(Summary = "Generate a random PDF document", Tags = new[] { "Documents" })]
    public IActionResult GeneratePdf(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("PDF request: {Path}", path ?? "index.pdf");

        var content = _pdfService.GeneratePdf();
        var fileName = string.IsNullOrEmpty(path) ? "index.pdf" : Path.GetFileName(path);
        if (!fileName.EndsWith(".pdf")) fileName += ".pdf";

        return File(content, "application/pdf", fileName);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("Word document request: {Path}", path ?? "index.docx");

        var content = _officeService.GenerateWordDocument();
        var fileName = string.IsNullOrEmpty(path) ? "index.docx" : Path.GetFileName(path);
        if (!fileName.EndsWith(".docx") && !fileName.EndsWith(".doc")) fileName += ".docx";

        return File(content, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("Excel document request: {Path}", path ?? "index.xlsx");

        var content = _officeService.GenerateExcelDocument();
        var fileName = string.IsNullOrEmpty(path) ? "index.xlsx" : Path.GetFileName(path);
        if (!fileName.EndsWith(".xlsx") && !fileName.EndsWith(".xls")) fileName += ".xlsx";

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("PowerPoint document request: {Path}", path ?? "index.pptx");

        var content = _officeService.GeneratePowerPointDocument();
        var fileName = string.IsNullOrEmpty(path) ? "index.pptx" : Path.GetFileName(path);
        if (!fileName.EndsWith(".pptx") && !fileName.EndsWith(".ppt")) fileName += ".pptx";

        return File(content, "application/vnd.openxmlformats-officedocument.presentationml.presentation", fileName);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        var format = "png";
        if (!string.IsNullOrEmpty(path))
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLower();
            if (new[] { "jpg", "jpeg", "png", "gif" }.Contains(ext))
            {
                format = ext;
            }
        }

        Logger.LogTrace("Image request: {Path} (format: {Format})", path ?? "random", format);

        var content = _imageService.GenerateImage(format);

        var mimeType = format switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "gif" => "image/gif",
            _ => "image/png"
        };

        // Return without filename to display inline instead of forcing download
        return File(content, mimeType);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("JSON request: {Path}", path ?? "index.json");

        var content = _dataFormatService.GenerateJson();
        return Content(content, "application/json");
    }

    [HttpGet("/csv")]
    [HttpPost("/csv")]
    [HttpGet("/csv/{**path}")]
    [HttpPost("/csv/{**path}")]
    [SwaggerOperation(Summary = "Generate random CSV data", Tags = new[] { "Data Structures" })]
    public IActionResult GenerateCsv(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("CSV request: {Path}", path ?? "index.csv");

        var content = _dataFormatService.GenerateCsv();
        var fileName = string.IsNullOrEmpty(path) ? "index.csv" : Path.GetFileName(path);
        if (!fileName.EndsWith(".csv")) fileName += ".csv";

        return File(content, "text/csv", fileName);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("Text request: {Path}", path ?? "index.txt");

        var content = _dataFormatService.GenerateText();
        return Content(content, "text/plain");
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

        Logger.LogTrace("HTML request: {Path}", path ?? "index.html");

        // If in social mode, return the themed Index view
        if (_configuration.Mode.Type.Equals("social", StringComparison.OrdinalIgnoreCase))
        {
            // Redirect to root to show the proper themed social feed
            return RedirectToAction("Index", "Posts");
        }

        // Website mode - return standalone HTML
        var content = _dataFormatService.GenerateHtml();
        return Content(content, "text/html");
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("JavaScript request: {Path}", path ?? "script.js");

        var content = _dataFormatService.GenerateScript();
        return Content(content, "application/javascript");
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("CSS request: {Path}", path ?? "styles.css");

        var content = _dataFormatService.GenerateStylesheet();
        return Content(content, "text/css");
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("ZIP request: {Path}", path ?? "archive.zip");

        var content = _archiveService.GenerateZip();
        var fileName = string.IsNullOrEmpty(path) ? "archive.zip" : Path.GetFileName(path);
        if (!fileName.EndsWith(".zip")) fileName += ".zip";

        return File(content, "application/zip", fileName);
    }

    [HttpGet("/tar")]
    [HttpPost("/tar")]
    [HttpGet("/tar/{**path}")]
    [HttpPost("/tar/{**path}")]
    [SwaggerOperation(Summary = "Generate a random TAR archive", Tags = new[] { "Archives" })]
    public IActionResult GenerateTar(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("TAR request: {Path}", path ?? "archive.tar");

        var content = _archiveService.GenerateTar();
        var fileName = string.IsNullOrEmpty(path) ? "archive.tar" : Path.GetFileName(path);
        if (!fileName.EndsWith(".tar")) fileName += ".tar";

        return File(content, "application/x-tar", fileName);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        var format = "mp4";
        if (!string.IsNullOrEmpty(path))
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLower();
            if (new[] { "mp4", "avi", "mov", "mkv", "webm" }.Contains(ext))
            {
                format = ext;
            }
        }

        Logger.LogTrace("Video request: {Path} (format: {Format})", path ?? "video.mp4", format);

        var content = _videoService.GenerateVideo(format);

        // Return without filename to allow inline playback
        return File(content, "video/mp4");
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        var format = "wav";
        if (!string.IsNullOrEmpty(path))
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLower();
            if (new[] { "wav", "mp3", "ogg", "m4a" }.Contains(ext))
            {
                format = ext;
            }
        }

        Logger.LogTrace("Audio request: {Path} (format: {Format})", path ?? "audio.wav", format);

        var content = _audioService.GenerateAudio(format);

        var mimeType = format switch
        {
            "wav" => "audio/wav",
            "mp3" => "audio/mpeg",
            "ogg" => "audio/ogg",
            "m4a" => "audio/mp4",
            _ => "audio/wav"
        };

        // Return without filename to allow inline playback
        return File(content, mimeType);
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
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("Binary file request: {Path}", path ?? "file.bin");

        var content = _binaryService.GenerateBinary();
        var fileName = string.IsNullOrEmpty(path) ? "file.bin" : Path.GetFileName(path);
        if (!fileName.EndsWith(".bin")) fileName += ".bin";

        return File(content, "application/octet-stream", fileName);
    }

    [HttpGet("/onenote")]
    [HttpPost("/onenote")]
    [HttpGet("/onenote/{**path}")]
    [HttpPost("/onenote/{**path}")]
    [SwaggerOperation(Summary = "Generate a random OneNote file", Tags = new[] { "Documents" })]
    public IActionResult GenerateOneNote(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("OneNote file request: {Path}", path ?? "notebook.one");

        var content = _binaryService.GenerateOneNote();
        var fileName = string.IsNullOrEmpty(path) ? "notebook.one" : Path.GetFileName(path);
        if (!fileName.EndsWith(".one")) fileName += ".one";

        return File(content, "application/onenote", fileName);
    }

    [HttpGet("/exe")]
    [HttpPost("/exe")]
    [HttpGet("/exe/{**path}")]
    [HttpPost("/exe/{**path}")]
    [SwaggerOperation(Summary = "Generate a fake executable file", Tags = new[] { "Executables" })]
    public IActionResult GenerateExe(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("EXE file request: {Path}", path ?? "program.exe");

        var content = _binaryService.GenerateExecutable("exe");
        var fileName = string.IsNullOrEmpty(path) ? "program.exe" : Path.GetFileName(path);
        if (!fileName.EndsWith(".exe")) fileName += ".exe";

        return File(content, "application/octet-stream", fileName);
    }

    [HttpGet("/msi")]
    [HttpPost("/msi")]
    [HttpGet("/msi/{**path}")]
    [HttpPost("/msi/{**path}")]
    [SwaggerOperation(Summary = "Generate a fake MSI installer file", Tags = new[] { "Executables" })]
    public IActionResult GenerateMsi(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("MSI file request: {Path}", path ?? "installer.msi");

        var content = _binaryService.GenerateExecutable("msi");
        var fileName = string.IsNullOrEmpty(path) ? "installer.msi" : Path.GetFileName(path);
        if (!fileName.EndsWith(".msi")) fileName += ".msi";

        return File(content, "application/x-msdownload", fileName);
    }

    [HttpGet("/iso")]
    [HttpPost("/iso")]
    [HttpGet("/iso/{**path}")]
    [HttpPost("/iso/{**path}")]
    [SwaggerOperation(Summary = "Generate a fake ISO disk image", Tags = new[] { "Disk Images" })]
    public IActionResult GenerateIso(string path = null)
    {
        if (!_configuration.Pandora.Enabled)
            return NotFound("Pandora is not enabled");

        Logger.LogTrace("ISO file request: {Path}", path ?? "disk.iso");

        var content = _binaryService.GenerateIso();
        var fileName = string.IsNullOrEmpty(path) ? "disk.iso" : Path.GetFileName(path);
        if (!fileName.EndsWith(".iso")) fileName += ".iso";

        return File(content, "application/octet-stream", fileName);
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
