using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Socializer.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/user")]
[Route("/u")]
[Route("/users")]
[Route("/profile")]
public class UsersController(ILogger logger, IWebHostEnvironment env, IUserService userService)
    : BaseController(logger)
{
    [HttpGet]
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUser(string username = null)
    {
        User user;
        if (string.IsNullOrEmpty(username))
            user = await userService.GetUserByUsernameAsync(CookieRead("username"));
        else
            user = await userService.GetUserByUsernameAsync(username);
        return View("Profile", user);
    }

    [HttpGet("{username}/avatar")]
    public IActionResult GetUserAvatar(string username)
    {
        Logger.LogTrace("{RequestScheme}://{RequestHost}{RequestPath}{RequestQueryString}|{RequestMethod}|",
            Request.Scheme, Request.Host, Request.Path, Request.QueryString, Request.Method);

        if (string.IsNullOrEmpty(username))
        {
            return PhysicalFile(Path.Combine(env.WebRootPath, "img", "avatar1.webp"), "image/webp");
        }

        var imageDir = Path.Combine(env.WebRootPath, "images", "u", username);
        var imagePath = Path.Combine(imageDir, "avatar.webp");

        if (!System.IO.File.Exists(imagePath))
        {
            Directory.CreateDirectory(imageDir);

            var rnd = new Random();
            var number = rnd.Next(1, 85);

            var sourceImagePath = Path.Combine(env.WebRootPath, "img", $"avatar{number}-sm.webp");
            if (System.IO.File.Exists(sourceImagePath))
            {
                try
                {
                    System.IO.File.Copy(sourceImagePath, imagePath);
                }
                catch (IOException ex)
                {
                    // with multiple writers this can exist even though existance previously checked
                    // so do not error on already exists
                    if (!ex.ToString().Contains("already exists"))
                    {
                        Logger.LogError(ex, "Error copying file.");
                        return StatusCode(500, "Internal Server Error");
                    }
                }
            }
            else
            {
                Logger.LogWarning($"Source avatar image not found: {sourceImagePath}");
                return NotFound("Avatar image not available.");
            }
        }

        return PhysicalFile(imagePath, "image/webp");
    }
}
