using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("{*catchall}")]
public class HomeController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext) : BaseController(logger, hubContext, dbContext)
{
    [HttpGet]
    public IActionResult Index()
    {
        var view = "index";
        var path = (Request.Path.Value ?? "").ToLowerInvariant();
        if (path.EndsWith("/detail") || path.StartsWith("/detail"))
            view = "detail";

        if (path.EndsWith("/profile") || path.StartsWith("/profile"))
            view = "profile";

        var posts = Db.Posts
            .Include(x => x.Likes)
            .Include(x => x.User)
            .Include(x => x.Theme)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(Program.Configuration.DefaultDisplay)
            .ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return View(view, posts);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] FilesController.FileInputModel model)
    {
        var post = new Post
        {
            Id = Guid.NewGuid().ToString(),
            CreatedUtc = DateTime.UtcNow
        };

        string? username = null;
        var userFormValues = new[] { "user", "usr", "u", "uid", "user_id", "u_id" };
        foreach (var userFormValue in userFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[userFormValue])) continue;
            username = Request.Form[userFormValue]!;
            break;
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
        var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                DisplayName = username,
                Bio = $"User {username}",
                Avatar = $"/u/{username}/avatar",
                Status = "online",
                CreatedUtc = DateTime.UtcNow,
                LastActiveUtc = DateTime.UtcNow
            };
            Db.Users.Add(user);
            await Db.SaveChangesAsync();
        }

        // Get default theme (facebook for now, or detect from request)
        var theme = await Db.Themes.FirstOrDefaultAsync(t => t.Name == "facebook");
        if (theme == null)
        {
            theme = new Theme
            {
                Name = "facebook",
                DisplayName = "Facebook",
                Description = "Facebook theme"
            };
            Db.Themes.Add(theme);
            await Db.SaveChangesAsync();
        }

        post.UserId = user.Id;
        post.ThemeId = theme.Id;

        // has the same user tried to post the same message within the past x minutes?
        if (Db.Posts.Any(_ =>
                _.Message.ToLower() == post.Message.ToLower()
                && _.UserId == user.Id
                && _.CreatedUtc > post.CreatedUtc.AddMinutes(-Program.Configuration.MinutesToCheckForDuplicatePost)))
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
            post.Message += $" <img src=\"{imagePath}\"/>";
        }

        Db.Posts.Add(post);
        await Db.SaveChangesAsync();

        CookieWrite("userid", username);

        await HubContext.Clients.All.SendAsync("SendMessage", post.Id, username, post.Message, post.CreatedUtc);

        return NoContent();
    }
}
