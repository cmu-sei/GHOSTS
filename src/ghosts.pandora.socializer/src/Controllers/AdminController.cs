using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("admin")]
public class AdminController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext) : BaseController(logger, hubContext, dbContext)
{
    [HttpGet("delete")]
    public async Task<IActionResult> Delete()
    {
        Db.Posts.RemoveRange(Db.Posts);
        await Db.SaveChangesAsync();
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
            var user = await Db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = username,
                    DisplayName = username,
                    Bio = $"User {username}",
                    Avatar = $"/u/{username}/avatar",
                    Status = "online",
                    CreatedUtc = DateTime.UtcNow,
                    LastActiveUtc = DateTime.UtcNow
                };
                Db.Users.Add(user);
                await Db.SaveChangesAsync();
            }

            // Get default theme
            var theme = await Db.Themes.FirstOrDefaultAsync(t => t.Name == "facebook");
            if (theme == null)
            {
                theme = new Theme
                {
                    Name = "facebook",
                    DisplayName = "Facebook",
                    Description = "Facebook theme"
                };
                Db.Themes.Add(theme);
                await Db.SaveChangesAsync();
            }

            var post = new Post
            {
                Id = Guid.NewGuid().ToString(),
                CreatedUtc = DateTime.MinValue.Add(
                    TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks)))),
                UserId = user.Id,
                ThemeId = theme.Id,
                Message = Faker.Lorem.Sentence(15)
            };
            Db.Posts.Add(post);
        }

        await Db.SaveChangesAsync();

        return NoContent();
    }
}
