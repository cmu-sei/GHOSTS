﻿using System.Net;
using Ghosts.Socializer.Hubs;
using Ghosts.Socializer.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Socializer.Controllers.Api;

[Route("/api/admin")]
[SwaggerTag("Administration functions")]
public class AdminController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext, ApplicationConfiguration applicationConfiguration) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Resets server by deleting all posts",
        Description = "Deletes all server data.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpDelete("delete")]
    public async Task<IActionResult> Delete()
    {
        dbContext.Posts.RemoveRange(dbContext.Posts);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [SwaggerOperation(
        Summary = "Generate some number of posts",
        Description = "Generates a list of posts using Faker.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpPost("generate/{n}")]
    public async Task<IActionResult> Generate(int n)
    {
        var r = new Random();

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
                Theme = user.Theme,
                Message = Faker.Lorem.Sentence(15)
            };
            dbContext.Posts.Add(post);
        }
        await dbContext.SaveChangesAsync();

        return NoContent();
    }
}
