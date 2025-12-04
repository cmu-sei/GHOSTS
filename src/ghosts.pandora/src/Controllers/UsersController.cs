using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/user")]
[Route("/u")]
[Route("/users")]
[Route("/profile")]
public class UsersController(
    ILogger logger,
    IWebHostEnvironment env,
    IUserService userService,
    IPostService postService,
    IFollowService followService)
    : BaseController(logger)
{
    [HttpGet]
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUser(string username = null)
    {
        var currentUsername = UserRead();
        var targetUsername = string.IsNullOrWhiteSpace(username) ? currentUsername : username;

        if (string.IsNullOrWhiteSpace(targetUsername))
        {
            return RedirectToAction("Index", "Home");
        }

        var requestedTheme = HttpContext.Request.Query["theme"].ToString();
        var theme = !string.IsNullOrWhiteSpace(requestedTheme) ? requestedTheme : ThemeRead();
        var user = await userService.GetUserByUsernameAsync(targetUsername, theme);
        if (user == null)
        {
            user = await userService.GetUserByUsernameAsync(targetUsername);
            theme = user?.Theme ?? theme;
        }

        if (user == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = user.Theme ?? "default";
        }

        ThemeWrite(theme);

        var isSelf = !string.IsNullOrWhiteSpace(currentUsername) &&
                     string.Equals(currentUsername, user.Username, StringComparison.OrdinalIgnoreCase);
        var isFollowing = false;
        if (!isSelf && !string.IsNullOrWhiteSpace(currentUsername))
        {
            var currentUser = await userService.GetUserByUsernameAsync(currentUsername, theme);
            if (currentUser != null)
            {
                isFollowing = await followService.IsFollowingAsync(currentUser.Id, user.Id);
            }
        }

        ViewBag.Theme = theme;
        ViewBag.ProfileUser = user.Username;
        ViewBag.ProfileUsername = user.Username;
        ViewBag.IsSelf = isSelf;
        ViewBag.IsFollowing = isFollowing;

        var viewPath = $"~/Views/Themes/{theme}/profile.cshtml";

        if (string.Equals(theme, "default", StringComparison.OrdinalIgnoreCase))
        {
            var posts = await postService.GetPostsByUserAndThemeAsync(user.Username, theme, 100);
            return View(viewPath, posts);
        }

        return View(viewPath, user);
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
