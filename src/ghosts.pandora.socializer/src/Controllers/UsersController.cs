using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("u")]
public class UsersController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext,
        IWebHostEnvironment env)
    : BaseController(logger, hubContext, dbContext)
{
    [HttpGet("{userId}")]
    public new virtual IActionResult User(string userId)
    {
        var posts = Db.Posts.Include(x => x.Likes).Where(x => x.User.Equals(userId, StringComparison.CurrentCultureIgnoreCase)).OrderByDescending(x => x.CreatedUtc)
            .Take(Program.Configuration.DefaultDisplay).ToList();

        ViewBag.User = userId;
        return View("Index", posts);
    }

    [HttpGet("{userId}/avatar")]
    public IActionResult GetUserAvatar(string userId)
    {
        Logger.LogTrace("{RequestScheme}://{RequestHost}{RequestPath}{RequestQueryString}|{RequestMethod}|",
            Request.Scheme, Request.Host, Request.Path, Request.QueryString, Request.Method);

        if (string.IsNullOrEmpty(userId))
        {
            return PhysicalFile(Path.Combine(env.WebRootPath, "img", "avatar1.webp"), "image/webp");
        }

        var imageDir = Path.Combine(env.WebRootPath, "images", "u", userId);
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
