using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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

            var post = new Post
            {
                Id = Guid.NewGuid().ToString(),
                CreatedUtc =
                DateTime.MinValue.Add(
                    TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks)))),
                User = Faker.Internet.UserName(),
                Message = Faker.Lorem.Sentence(15)
            };
            Db.Posts.Add(post);
        }

        await Db.SaveChangesAsync();

        return NoContent();
    }
}
