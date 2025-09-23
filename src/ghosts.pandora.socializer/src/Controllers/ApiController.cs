using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ghosts.Socializer.Hubs;
using Ghosts.Socializer.Infrastructure;

namespace Ghosts.Socializer.Controllers;

[Route("/api")]
public class ApiController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext, ApplicationConfiguration applicationConfiguration) : BaseController(logger, hubContext, dbContext)
{
    [HttpGet]
    public IEnumerable<Post> Index()
    {
        var posts = dbContext.Posts
            .Include(x => x.Likes)
            .Include(x => x.User)
            .Include(x => x.Theme)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return posts;
    }

    [HttpGet("u/{userId}")]
    public new virtual IEnumerable<Post> User(string userId)
    {
        var posts = dbContext.Posts
            .Include(x => x.Likes)
            .Include(x => x.User)
            .Include(x => x.Theme)
            .Where(x => x.User.Username.Equals(userId, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return posts;
    }

    [HttpGet("{id:guid}")]
    public Post Detail(Guid id)
    {
        var post = dbContext.Posts
            .Include(x => x.Likes)
            .Include(x => x.User)
            .Include(x => x.Theme)
            .FirstOrDefault(x => x.Id == id);

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return post;
    }

    [HttpGet("/admin/delete")]
    public async Task<IActionResult> Delete()
    {
        dbContext.Posts.RemoveRange(dbContext.Posts);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("/admin/generate/{n}")]
    public async Task<IActionResult> Generate(int n)
    {
        var r = new Random();

        // Get or create default theme
        var theme = await dbContext.Themes.FirstOrDefaultAsync(t => t.Name == "facebook") ?? new Theme
        {
            Name = "facebook",
            DisplayName = "Facebook",
            Description = "Facebook theme"
        };
        if (theme.Id == 0)
        {
            dbContext.Themes.Add(theme);
            await dbContext.SaveChangesAsync();
        }

        for (var i = 0; i < n; i++)
        {
            var min = DateTime.Now.AddDays(-7);
            var username = Faker.Internet.UserName();

            // Get or create user
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = new User
                {
                    Username = username,
                    Bio = $"User {username}",
                    Avatar = $"/u/{username}/avatar",
                    Status = "online",
                    CreatedUtc = DateTime.UtcNow,
                    LastActiveUtc = DateTime.UtcNow
                };
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
            }

            var post = new Post
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.MinValue.Add(TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks)))),
                Username = user.Username,
                ThemeId = theme.Id,
                Message = Faker.Lorem.Sentence(15)
            };
            dbContext.Posts.Add(post);
        }
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] FilesController.FileInputModel model)
    {
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
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            user = new User
            {
                Username = username,
                Bio = $"User {username}",
                Avatar = $"/u/{username}/avatar",
                Status = "online",
                CreatedUtc = DateTime.UtcNow,
                LastActiveUtc = DateTime.UtcNow
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        // Get default theme
        var theme = await dbContext.Themes.FirstOrDefaultAsync(t => t.Name == "facebook");
        if (theme == null)
        {
            theme = new Theme
            {
                Name = "facebook",
                DisplayName = "Facebook",
                Description = "Facebook theme"
            };
            dbContext.Themes.Add(theme);
            await dbContext.SaveChangesAsync();
        }

        post.Username = user.Username;
        post.ThemeId = theme.Id;

        // has the same user tried to post the same message within the past x minutes?
        if (dbContext.Posts.Any(p => p.Message.Equals(post.Message, StringComparison.CurrentCultureIgnoreCase)
                                && p.Username == user.Username
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
            post.Message += $" <img src=\"{imagePath}\"/>";
        }

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();

        CookieWrite("userid", username);

        await HubContext.Clients.All.SendAsync("SendMessage", post.Id, username, post.Message, post.CreatedUtc);

        return NoContent();
    }
}
