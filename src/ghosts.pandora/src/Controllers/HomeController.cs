using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ghosts.Pandora.Hubs;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class HomeController(
    ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext,
    ApplicationConfiguration applicationConfiguration,
    IWebsiteGenerationService websiteGenerationService)
    : BaseController(logger)
{
    // Catch-all for unhandled routes - must be LAST in priority
    [HttpGet("{*catchall}")]
    public IActionResult CatchAll()
    {
        var path = Request.Path.Value ?? "";

        // Check if we're in website mode
        if (applicationConfiguration.Mode.Type.Equals("website", StringComparison.OrdinalIgnoreCase))
        {
            // If requesting root, serve website homepage
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                var homepage = websiteGenerationService.GenerateHomepage(
                    applicationConfiguration.Mode.SiteType,
                    applicationConfiguration.Mode.SiteName,
                    applicationConfiguration.Mode.ArticleCount
                );
                return Content(homepage, "text/html");
            }

            // For other paths in website mode, generate appropriate content or 404
            Logger.LogInformation("Website mode: serving path {Path}", path);

            // Return 404 for unhandled paths in website mode
            return NotFound();
        }

        // Social mode - original behavior
        // Log unhandled routes for debugging
        Logger.LogWarning("Unhandled route: {Path}", path);

        // Try to extract useful information from the path
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 0)
        {
            RedirectToAction(segments.Last(), "Posts");
        }

        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }
        var posts = dbContext.Posts
            .Include(x => x.User)
            .Include(x=>x.Comments.OrderByDescending(c => c.CreatedUtc)).ThenInclude(c => c.User)
            .Include(x=>x.Likes).ThenInclude(l => l.User)
            .Where(x=>x.Theme == theme)
            .OrderByDescending(x=>x.CreatedUtc).ToList();

        return View("Index", posts);
    }

    [HttpPost("{*catchall}")]
    public async Task<IActionResult> Post([FromForm] FilesController.FileInputModel model)
    {
        // Only handle form posts - skip if not a form submission
        if (!Request.HasFormContentType)
        {
            return NotFound();
        }

        var queryTheme = Request.Query["theme"].ToString();
        if (string.IsNullOrEmpty(queryTheme))
            queryTheme = ThemeRead();
        if (string.IsNullOrWhiteSpace(queryTheme))
            queryTheme = "default";

        var post = new Post
        {
            Id = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };

        string username = null;
        var userFormValues = new[] { "user", "usr", "u", "uid", "user_id", "u_id" };
        foreach (var userFormValue in userFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[userFormValue])) continue;
            username = Request.Form[userFormValue]!;
            break;
        }

        if (string.IsNullOrEmpty(username))
        {
            username = Faker.Internet.UserName();
            UserWrite(username);
        }

        var messageFormValues = new[] { "message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload" };
        foreach (var messageFormValue in messageFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[messageFormValue])) continue;
            post.Message = Request.Form[messageFormValue]!;
            break;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(post.Message))
            return BadRequest("User and message are required.");

        // Get or create user
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username && u.Theme == queryTheme);
        if (user == null)
        {
            user = new User
            {
                Username = username,
                Bio = $"User {username}",
                Avatar = $"/u/{username}/avatar",
                Status = "online",
                Theme = queryTheme,
                CreatedUtc = DateTime.UtcNow,
                LastActiveUtc = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        post.Username = user.Username;
        post.UserId = user.Id;
        post.Theme = queryTheme;
        post.CreatedOnUrl = Request.Path.Value;

        // has the same user tried to post the same message within the past x minutes?
        if (dbContext.Posts.Any(p =>
                p.Message.ToLower() == post.Message.ToLower()
                && p.UserId == user.Id
                && p.CreatedUtc > post.CreatedUtc.AddMinutes(-applicationConfiguration.MinutesToCheckForDuplicatePost)))
        {
            Logger.LogInformation("Client is posting duplicates: {PostUser}", username);
            return NoContent();
        }

        var imagePath = string.Empty;
        if (model.File != null)
        {
            var guid = Guid.NewGuid().ToString();
            var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            savePath = Path.Combine(savePath, guid);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            savePath = Path.Combine(savePath, model.File.FileName);

            try
            {
                // Process the file and save it to storage
                // Note: You may want to validate the file size, content type, etc. before saving it
                await using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

                imagePath = $"/images/{guid}/{model.File.FileName}";
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        if (!string.IsNullOrEmpty(imagePath))
        {
            post.Message += $"<br/><img src=\"{imagePath}\"/>";
        }

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();

        UserWrite(username);

        await hubContext.Clients.All.SendAsync("SendMessage", post.Id, username, post.Message, post.CreatedUtc);

        return NoContent();
    }
}
