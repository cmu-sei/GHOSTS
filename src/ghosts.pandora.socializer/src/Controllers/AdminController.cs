using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("admin")]
public class AdminController : BaseController
{
    public AdminController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext) :
        base(logger, hubContext, dbContext)
    {
    }
    
    [HttpGet("delete")]
    public async Task<IActionResult> Delete()
    {
        this.Db.Posts.RemoveRange(this.Db.Posts);
        await this.Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("generate/{n}")]
    public async Task<IActionResult> Generate(int n)
    {
        var r = new Random();
        for (var i = 0; i < n; i++)
        {
            var min = DateTime.Now.AddDays(-7);
            var randTicks = r.Next(0, (int)(DateTime.Now.Ticks - min.Ticks));

            var post = new Post();
            post.Id = Guid.NewGuid().ToString();
            post.CreatedUtc =
                DateTime.MinValue.Add(
                    TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks))));
            post.User = Faker.Internet.UserName();
            post.Message = Faker.Lorem.Sentence(15);
            this.Db.Posts.Add(post);
        }

        await Db.SaveChangesAsync();

        return NoContent();
    }
}