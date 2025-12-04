using Microsoft.AspNetCore.Mvc;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/r")]
[Route("/r/{subreddit}")]
[Route("/post")]
[Route("/posts")]
public class PostsController(ILogger logger, DataContext dbContext, IUserService userService) : BaseController(logger)
{
    [HttpGet]
    public IActionResult Index()
    {
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

    [HttpGet("{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }
        var post = dbContext.Posts
            .Include(x => x.User)
            .Include(x=>x.Comments.OrderByDescending(c => c.CreatedUtc)).ThenInclude(c => c.User)
            .Include(x=>x.Likes).ThenInclude(l => l.User)
            .FirstOrDefault(x=>x.Theme == theme && x.Id == id);

        return View("Detail", post);
    }

    [HttpGet("{id:guid}/likes")]
    public IEnumerable<Like> GetLikes(Guid id)
    {
        return dbContext.Likes
            .Include(l => l.User)
            .Where(x => x.PostId == id)
            .ToArray();
    }

    [HttpPost("{id:guid}/likes")]
    public async Task<IActionResult> Like(Guid id)
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        var user = await userService.GetOrCreateUserAsync(username, theme);

        var like = new Like
        {
            PostId = id,
            UserId = user.Id,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Likes.Add(like);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:guid}/comments")]
    public IEnumerable<Comment> GetComments(Guid id)
    {
        return dbContext.Comments
            .Include(c => c.User)
            .Where(x => x.PostId == id)
            .ToArray();
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<IActionResult> Comment(Guid id, [FromForm] FilesController.FileInputModel model)
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }
        var user = await userService.GetOrCreateUserAsync(username, theme);

        var comment = new Comment
        {
            PostId = id,
            UserId = user.Id,
            CreatedUtc = DateTime.UtcNow,
        };

        var messageFormValues = new[] { "message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload" };
        foreach (var messageFormValue in messageFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[messageFormValue])) continue;
            comment.Message = Request.Form[messageFormValue]!;
            break;
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
            comment.Message += $"<br/><img src=\"{imagePath}\"/>";
        }

        dbContext.Comments.Add(comment);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
