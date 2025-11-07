using Ghosts.Socializer.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Socializer.Controllers;

[ApiController]
[SwaggerTag("Payload delivery endpoints for configured files")]
public class PayloadController : BaseController
{
    private readonly ApplicationConfiguration _configuration;

    public PayloadController(
        ILogger<PayloadController> logger,
        ApplicationConfiguration configuration) : base(logger)
    {
        _configuration = configuration;
    }

    [HttpGet("/payloads/{**path}")]
    [HttpPost("/payloads/{**path}")]
    [SwaggerOperation(Summary = "Serve configured payload files", Tags = new[] { "Payloads" })]
    public IActionResult ServePayload(string path)
    {
        if (!_configuration.Payloads.Enabled)
        {
            Logger.LogWarning("Payloads are disabled");
            return NotFound("Payloads are not enabled");
        }

        var requestPath = $"/payloads/{path}";
        Logger.LogInformation("Payload request for path: {Path}", requestPath);

        // Find matching payload configuration
        var payload = _configuration.Payloads.Mappings
            .FirstOrDefault(p => requestPath.StartsWith(p.Url, StringComparison.OrdinalIgnoreCase));

        if (payload == null)
        {
            Logger.LogWarning("No payload mapping found for path: {Path}", requestPath);
            return NotFound("No payload configured for this path");
        }

        // Construct file path
        var payloadDir = Path.Combine(Directory.GetCurrentDirectory(), _configuration.Payloads.PayloadDirectory);
        var filePath = Path.Combine(payloadDir, payload.FileName);

        Logger.LogInformation("Serving payload file: {FilePath} with content type: {ContentType}",
            payload.FileName, payload.ContentType);

        // Check if file exists
        if (!System.IO.File.Exists(filePath))
        {
            Logger.LogError("Payload file not found: {FilePath}", filePath);
            return NotFound($"Payload file not found: {payload.FileName}");
        }

        try
        {
            // Read and serve the file
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            var fileName = Path.GetFileName(payload.FileName);

            Logger.LogInformation("Successfully serving payload: {FileName} ({Size} bytes)",
                fileName, fileBytes.Length);

            return File(fileBytes, payload.ContentType, fileName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error serving payload file: {FilePath}", filePath);
            return StatusCode(500, "Error serving payload file");
        }
    }

    [HttpGet("/payloads")]
    [SwaggerOperation(Summary = "List all configured payloads", Tags = new[] { "Payloads" })]
    public IActionResult ListPayloads()
    {
        if (!_configuration.Payloads.Enabled)
        {
            return NotFound("Payloads are not enabled");
        }

        return Ok(new
        {
            enabled = _configuration.Payloads.Enabled,
            payloadDirectory = _configuration.Payloads.PayloadDirectory,
            count = _configuration.Payloads.Mappings.Count,
            payloads = _configuration.Payloads.Mappings.Select(p => new
            {
                url = p.Url,
                fileName = p.FileName,
                contentType = p.ContentType,
                exists = System.IO.File.Exists(Path.Combine(
                    Directory.GetCurrentDirectory(),
                    _configuration.Payloads.PayloadDirectory,
                    p.FileName))
            })
        });
    }
}
