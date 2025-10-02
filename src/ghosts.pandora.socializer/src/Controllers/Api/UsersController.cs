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
    [HttpGet]
    public async Task<IEnumerable<User>> GetUsers(CancellationToken ct)
    {
        var users = await dbContext.Users.ToListAsync(ct);
        return users;
    }

    [HttpGet("{username}")]
    public async Task<IEnumerable<User>> GetUsersByUsername(string username, CancellationToken ct)
    {
        var users = await dbContext.Users.Where(x=>x.Username == username).ToListAsync(ct);
        return users;
    }

    [HttpGet("{id:guid}")]
    public async Task<User> GetUserById(Guid id, CancellationToken ct)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x=>x.Id == id, ct);
        return user;
    }

    [SwaggerOperation(
        Summary = "Retrieve all posts for a user",
        Description = "Returns a list of posts created by a username.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet("{username}/posts")]
    public IEnumerable<Post> PostsByUsername(string username)
    {
        var posts = dbContext.Posts
            .Include(x => x.Likes)
            .Where(x => x.User.Username.ToUpper() == username.ToUpper())
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        return posts;
    }

    [SwaggerOperation(
        Summary = "Retrieve all posts for a user",
        Description = "Returns a list of posts created by a username.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet("{id:guid}/posts")]
    public IEnumerable<Post> PostsByUserId(Guid id)
    {
        var posts = dbContext.Posts
            .Include(x => x.Likes)
            .Where(x => x.User.Id == id)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        return posts;
    }
}
