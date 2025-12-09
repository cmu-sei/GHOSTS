using Microsoft.AspNetCore.Mvc;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/r")]
[Route("/r/{subreddit}")]
[Route("/post")]
[Route("/posts")]
public class PostsController(ILogger logger, IPostService service, IUserService userService) : BaseController(logger)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        var posts = await service.GetPostsByTheme(theme);
        return View("Index", posts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id)
    {
        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        var post = await service.GetPostById(id);

        return View("Detail", post);
    }

    [HttpGet("{id:guid}/likes")]
    public async Task<IEnumerable<Like>> GetLikes(Guid id)
    {
        return await service.GetLikes(id);
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

        await service.LikePost(id, user.Id);
        return NoContent();
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<IEnumerable<Comment>> GetComments(Guid id)
    {
        return await service.GetComments(id);
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

        await service.CreateComment(comment.PostId, comment.UserId, comment.Message);
        return NoContent();
    }
}
