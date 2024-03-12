using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace ghosts.api.Controllers;

/// <summary>
/// For installing client binaries from the api
/// </summary>
[Route("/install")]
public class InstallController : Controller
{
    [HttpGet("client/windows")]
    [HttpGet("client/windows/x64")]
    public IActionResult Windows64Client()
    {
        return GetFile("config/binaries/windows/x64");
    }
    
    [HttpGet("client/windows/x32")]
    public IActionResult Windows32Client()
    {
        return GetFile("config/binaries/windows/x32");
    }
    
    [HttpGet("client/linux")]
    public IActionResult LinuxClient()
    {
        return GetFile("config/binaries/linux");
    }

    private IActionResult GetFile(string path)
    {
        if (!Directory.Exists(path) || !Directory.EnumerateFiles(path, "*.zip").Any()) return NotFound("Binary file not found.");
        
        var zipFilePath = Directory.EnumerateFiles(path, "*.zip").First();
        var fileName = Path.GetFileName(zipFilePath);
        return File(System.IO.File.ReadAllBytes(zipFilePath), "application/zip", fileName);
    }
}