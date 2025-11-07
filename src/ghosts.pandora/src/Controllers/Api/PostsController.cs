using System.Net;
using Ghosts.Pandora.Hubs;
using Ghosts.Pandora.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/posts")]
[SwaggerTag("API Functionality for posts")]
public class PostsController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext, ApplicationConfiguration applicationConfiguration) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Retrieve all posts",
        Description = "Returns a list of all posts.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet]
    public IEnumerable<Post> Posts()
    {
        var posts = dbContext.Posts
            //.Include(x => x.Likes)
            //.Include(x=>x.Comments.OrderByDescending(c => c.CreatedUtc))
            //.Include(x => x.User)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        return posts;
    }

    [HttpGet("{id:guid}/detail")]
    public Post Detail(Guid id)
    {
        var post = dbContext.Posts
            .Include(x => x.Likes)
            .Include(x=>x.Comments.OrderByDescending(c => c.CreatedUtc))
            .Include(x => x.User)
            .FirstOrDefault(x => x.Id == id);

        return post;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] FilesController.FileInputModel model)
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow
        };

        string username = null;
        var userFormValues = new[] { "user", "usr", "u", "uid", "user_id", "u_id" };
        foreach (var userFormValue in userFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[userFormValue])) continue;
            username = Request.Form[userFormValue]!;
            break;
        }

        var messageFormValues = new[] { "message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload" };
        foreach (var messageFormValue in messageFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[messageFormValue])) continue;
            post.Message = Request.Form[messageFormValue]!;
            break;
        }

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(post.Message))
            return BadRequest("User and message are required.");

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

        post.Username = user.Username;
        post.Theme = user.Theme;

        // has the same user tried to post the same message within the past x minutes?
        if (dbContext.Posts.Any(p => p.Message.Equals(post.Message, StringComparison.CurrentCultureIgnoreCase)
                                && p.Username == user.Username
                                && p.CreatedUtc > post.CreatedUtc.AddMinutes(-applicationConfiguration.MinutesToCheckForDuplicatePost)))
        {
            Logger.LogInformation("Client is posting duplicates: {PostUser}", username);
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

        dbContext.Posts.Add(post);
        await dbContext.SaveChangesAsync();

        UserWrite(username);

        await hubContext.Clients.All.SendAsync("SendMessage", post.Id, username, post.Message, post.CreatedUtc);

        return NoContent();
    }
}
