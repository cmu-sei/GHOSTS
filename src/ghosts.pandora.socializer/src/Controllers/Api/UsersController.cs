using System.Net;
using Ghosts.Socializer.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Socializer.Controllers.Api;

[Route("/api/users")]
[SwaggerTag("API Functionality for users.")]
public class UsersController(ILogger logger, DataContext dbContext, ApplicationConfiguration applicationConfiguration) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Retrieve all posts for a user",
        Description = "Returns a list of posts created by a username.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet("{username}/posts")]
    public IEnumerable<Post> PostsByUser(string username)
    {
        var posts = dbContext.Posts
            .Include(x => x.Likes)
            .Include(x => x.User)
            .Include(x => x.Theme)
            .Where(x => x.User.Username.Equals(username, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return posts;
    }
}
