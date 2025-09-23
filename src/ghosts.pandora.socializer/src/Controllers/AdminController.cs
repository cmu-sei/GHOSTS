using Microsoft.AspNetCore.Mvc;
using Ghosts.Socializer.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ghosts.Socializer.Controllers;

[Route("admin")]
public class AdminController(ILogger logger, DataContext dbContext) : BaseController(logger)
{
    [HttpGet("delete")]
    public async Task<IActionResult> Delete()
    {
        dbContext.Posts.RemoveRange(dbContext.Posts);
        await dbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("generate/{n}")]
    public async Task<IActionResult> Generate(int n)
    {
        var r = new Random();
        for (var i = 0; i < n; i++)
        {
            var min = DateTime.Now.AddDays(-7);
            _ = r.Next(0, (int)(DateTime.Now.Ticks - min.Ticks));

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
                CreatedUtc = DateTime.MinValue.Add(
                    TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks)))),
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
