using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("/api")]
public class ApiController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext) : BaseController(logger, hubContext, dbContext)
{
    [HttpGet]
    public IEnumerable<Post> Index()
    {
        var posts = Db.Posts.Include(x => x.Likes).OrderByDescending(x => x.CreatedUtc).Take(Program.Configuration.DefaultDisplay).ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return posts;
    }

    [HttpGet("u/{userId}")]
    public new virtual IEnumerable<Post> User(string userId)
    {
        var posts = Db.Posts.Include(x => x.Likes).Where(x => x.User.Equals(userId, StringComparison.CurrentCultureIgnoreCase)).OrderByDescending(x => x.CreatedUtc).Take(Program.Configuration.DefaultDisplay).ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return posts;
    }

    [HttpGet("{id:guid}")]
    public Post? Detail(Guid id)
    {
        var post = Db.Posts.Include(x => x.Likes).FirstOrDefault(x => x.Id == id.ToString());

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return post;
    }

    [HttpGet("/admin/delete")]
    public async Task<IActionResult> Delete()
    {
        Db.Posts.RemoveRange(Db.Posts);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("/admin/generate/{n}")]
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
                CreatedUtc = DateTime.MinValue.Add(TimeSpan.FromTicks(min.Ticks + (long)(r.NextDouble() * (DateTime.Now.Ticks - min.Ticks)))),
                User = Faker.Internet.UserName(),
                Message = Faker.Lorem.Sentence(15)
            };
            Db.Posts.Add(post);
        }
        await Db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] FilesController.FileInputModel model)
    {
        var post = new Post
        {
            Id = Guid.NewGuid().ToString(),
            CreatedUtc = DateTime.UtcNow
        };

        var userFormValues = new[] { "user", "usr", "u", "uid", "user_id", "u_id" };
        foreach (var userFormValue in userFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[userFormValue])) continue;
            post.User = Request.Form[userFormValue]!;
            break;
        }

        var messageFormValues = new[] { "message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload" };
        foreach (var messageFormValue in messageFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[messageFormValue])) continue;
            post.Message = Request.Form[messageFormValue]!;
            break;
        }

        if (string.IsNullOrEmpty(post.User) || string.IsNullOrEmpty(post.Message))
            return BadRequest("User and message are required.");

        // has the same user tried to post the same message within the past x minutes?
        if (Db.Posts.Any(_ => _.Message.Equals(post.Message, StringComparison.CurrentCultureIgnoreCase)
                                && _.User.Equals(post.User, StringComparison.CurrentCultureIgnoreCase)
                                && _.CreatedUtc > post.CreatedUtc.AddMinutes(-Program.Configuration.MinutesToCheckForDuplicatePost)))
        {
            Logger.LogInformation("Client is posting duplicates: {PostUser}", post.User);
            return NoContent();
        }

        var imagePath = string.Empty;
        if (model.File != null)
        {
            var guid = Guid.NewGuid().ToString();
            var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);
            savePath = Path.Combine(savePath, guid);
            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            savePath = Path.Combine(savePath, model.File.FileName);

            try
            {
                // Process the file and save it to storage
                // Note: You may want to validate the file size, content type, etc. before saving it
                await using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await model.File.CopyToAsync(stream);
                }

                imagePath = $"/images/{guid}/{model.File.FileName}";
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        if (!string.IsNullOrEmpty(imagePath))
        {
            post.Message += $" <img src=\"{imagePath}\"/>";
        }

        Db.Posts.Add(post);
        await Db.SaveChangesAsync();

        CookieWrite("userid", post.User);

        await HubContext.Clients.All.SendAsync("SendMessage", post.Id, post.User, post.Message, post.CreatedUtc);

        return NoContent();
    }
}
