using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Ghosts.Pandora.Hubs;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
public class HomeController(
    ILogger logger, IHubContext<PostsHub> hubContext,
    IPostService postService, IUserService userService,
    ApplicationConfiguration applicationConfiguration,
    IWebsiteGenerationService websiteGenerationService)
    : BaseController(logger)
{
    // Catch-all for unhandled routes - must be LAST in priority
    [HttpGet("{*catchall}")]
    public async Task<IActionResult> CatchAll()
    {
        var path = Request.Path.Value ?? "";

        // Check if we're in website mode
        if (applicationConfiguration.Mode.Type.Equals("website", StringComparison.OrdinalIgnoreCase))
        {
            // Every page the site links to exists; anything else gets the site's own not-found page
            var page = websiteGenerationService.RenderPage(path);
            if (page != null)
                return Content(page, "text/html; charset=utf-8");

            Logger.LogInformation("Website mode: no page at {Path}", ForLog(path));
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Content(websiteGenerationService.NotFoundPage(), "text/html; charset=utf-8");
        }

        // Social mode - original behavior
        // Log unhandled routes for debugging
        Logger.LogWarning("Unhandled route: {Path}", ForLog(path));

        // Try to extract useful information from the path
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length > 0)
        {
            RedirectToAction(segments.Last(), "Posts");
        }

        var theme = ThemeRead();
        var posts = await postService.GetLatestPostsByTheme(theme);

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
        queryTheme = string.IsNullOrEmpty(queryTheme) ? ThemeRead() : KnownTheme(queryTheme);

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

        if (string.IsNullOrWhiteSpace(username))
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
        var user = await userService.GetOrCreateUserAsync(username, queryTheme);

        post.Username = user.Username;
        post.UserId = user.Id;
        post.Theme = queryTheme;
        post.CreatedOnUrl = Request.Path.Value;

        var imagePath = string.Empty;
        if (model.File != null)
        {
            try
            {
                imagePath = await model.SaveImageAsync();
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

        await postService.CreatePost(post);

        UserWrite(username);

        await hubContext.Clients.All.SendAsync("SendMessage", post.Id, username, post.Theme, MessageHtml.ToHtml(post.Message), post.CreatedUtc);

        return NoContent();
    }
}
