using System.Net;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/users")]
[SwaggerTag("API Functionality for users.")]
public class UsersController(ILogger logger, IUserService service, IPostService postService) : BaseController(logger)
{
    [HttpGet]
    public async Task<IEnumerable<User>> GetUsers(CancellationToken ct)
    {
        return await service.GetAllUsersAsync();
    }

    [HttpGet("{username}")]
    public async Task<User> GetUsersByUsername(string username, string theme, CancellationToken ct)
    {
        return await service.GetUserByUsernameAsync(username, theme);
    }

    [HttpGet("{id:guid}")]
    public async Task<User> GetUserById(Guid id, CancellationToken ct)
    {
        return await service.GetUserByIdAsync(id);
    }

    [SwaggerOperation(
        Summary = "Set the bio and status of a user",
        Description = "Creates the user if needed, then sets whichever of bio and status was sent.",
        OperationId = "UpdateUser")]
    [ProducesResponseType(typeof(User), (int)HttpStatusCode.OK)]
    [HttpPost("{username}")]
    public async Task<User> UpdateUser(string username, [FromForm] string bio, [FromForm] string status)
    {
        var theme = Request.Query["theme"].ToString();
        if (string.IsNullOrWhiteSpace(theme))
            theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
            theme = "default";

        await service.GetOrCreateUserAsync(username, theme);
        return await service.UpdateUserAsync(username, theme, bio, status);
    }

    [SwaggerOperation(
        Summary = "Retrieve all posts for a user",
        Description = "Returns a list of posts created by a username.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet("{username}/posts")]
    public async Task<IEnumerable<Post>> PostsByUsername(string username, string theme, CancellationToken ct)
    {
        return await postService.GetPostsByUserAndTheme(username, theme);
    }

    [SwaggerOperation(
        Summary = "Retrieve all posts for a user",
        Description = "Returns a list of posts created by a username.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet("{id:guid}/posts")]
    public async Task<IEnumerable<Post>> PostsByUserId(Guid id)
    {
        return await postService.GetPostsByUserId(id);
    }
}
