using System.Net;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/admin")]
[SwaggerTag("Administration functions")]
public class AdminController(ILogger logger, IPostService service, IUserService userService) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Resets server by deleting all posts",
        Description = "Deletes all server data.",
        OperationId = "ResetServer")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete()
    {
        // await service.RemovePosts();
        // foreach(var user in await userService.GetAllUsersAsync())
        //     await userService.DeleteUserAsync(user.Username);
        return NoContent();
    }

    [SwaggerOperation(
        Summary = "Generate some number of posts",
        Description = "Generates a list of posts using Faker.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpPost("generate/{n}")]
    public async Task<IActionResult> Generate(int n, string theme)
    {
        var r = new Random();

        for (var i = 0; i < n; i++)
        {
            var min = DateTime.Now.AddDays(-7);
            var username = Faker.Internet.UserName();

            // Get or create user
            var user = await userService.GetOrCreateUserAsync(username, theme);

            var post = new Post
            {
                Id = Guid.NewGuid(),
                CreatedUtc = DateTime.MinValue.Add(TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks)))),
                Username = user.Username,
                UserId = user.Id,
                Theme = user.Theme,
                Message = Faker.Lorem.Sentence(15)
            };
            await service.CreatePost(post.UserId, user.Username, post.Theme, post.Message);
        }

        return NoContent();
    }
}
