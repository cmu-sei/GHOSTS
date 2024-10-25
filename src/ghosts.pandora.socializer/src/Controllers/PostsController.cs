using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("posts")]
public class PostsController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext) : BaseController(logger, hubContext, dbContext)
{
    [HttpGet("{id:guid}")]
    public IActionResult Detail(Guid id)
    {
        var post = Db.Posts.Include(x => x.Likes).FirstOrDefault(x => x.Id == id.ToString());

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return View(post);
    }

    [HttpGet("{id:guid}/likes")]
    public IEnumerable<Like> GetLikes(string id)
    {
        return Db.Likes.Where(x => x.PostId == id).ToArray();
    }

    [HttpPost("{id:guid}/likes")]
    public IActionResult Like(string id)
    {
        var userId = CookieRead("userid");
        if (string.IsNullOrEmpty(userId))
        {
            return NoContent();
        }

        var like = new Like
        {
            PostId = id,
            UserId = userId,
            CreatedUtc = DateTime.UtcNow
        };

        Db.Likes.Add(like);
        Db.SaveChanges();
        return NoContent();
    }
}
