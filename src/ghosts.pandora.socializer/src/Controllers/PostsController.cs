using Microsoft.AspNetCore.Mvc;
using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Services;

namespace Ghosts.Socializer.Controllers;

[Route("/r")]
[Route("/r/{subreddit}")]
[Route("/posts")]
public class PostsController(ILogger logger, DataContext dbContext, IUserService userService) : BaseController(logger)
{
    [HttpGet]
    public IActionResult Index()
    {
        var theme = ThemeRead();
        var posts = dbContext.Posts.Where(x=>x.Theme == theme).OrderByDescending(x=>x.CreatedUtc).ToList();

        return View("Index", posts);
    }

    [HttpGet("{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        var theme = ThemeRead();
        var post = dbContext.Posts.Where(x=>x.Theme == theme && x.Id == id);

        return View("detail", post);
    }

    [HttpGet("/api/posts/{id:guid}/likes")]
    public IEnumerable<Like> GetLikes(Guid id)
    {
        return dbContext.Likes.Where(x => x.PostId == id).ToArray();
    }

    [HttpPost("/api/posts/{id:guid}/likes")]
    public async Task<IActionResult> Like(Guid id)
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);

        await userService.CreateUserAsync(username);

        var like = new Like
        {
            PostId = id,
            Username = username,
            CreatedUtc = DateTime.UtcNow
        };

        dbContext.Likes.Add(like);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }
}
