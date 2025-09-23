using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Ghosts.Socializer.Hubs;
using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Services;

namespace Ghosts.Socializer.Controllers;

[Route("posts")]
public class PostsController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext, IUserService userService) : BaseController(logger, hubContext, dbContext)
{
    [HttpGet("{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        var post = Db.Posts.Include(x => x.Likes).FirstOrDefault(x => x.Id == id);

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return View(post);
    }

    [HttpGet("{id:guid}/likes")]
    public IEnumerable<Like> GetLikes(Guid id)
    {
        return Db.Likes.Where(x => x.PostId == id).ToArray();
    }

    [HttpPost("{id:guid}/likes")]
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

        Db.Likes.Add(like);
        await Db.SaveChangesAsync();
        return NoContent();
    }
}
