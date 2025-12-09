using System.Net;
using Ghosts.Pandora.Hubs;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/posts")]
[SwaggerTag("API Functionality for posts")]
public class PostsController(ILogger logger, IHubContext<PostsHub> hubContext, IPostService service,
    IUserService userService, ApplicationConfiguration applicationConfiguration) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Retrieve all posts",
        Description = "Returns a list of all posts.",
        OperationId = "GetPosts")]
    [ProducesResponseType(typeof(IEnumerable<Post>), (int)HttpStatusCode.OK)]
    [HttpGet]
    public async Task<IEnumerable<Post>> Posts()
    {
        return await service.GetAllPosts(applicationConfiguration.DefaultDisplay, 0);
    }

    [HttpGet("{id:guid}/detail")]
    public async Task<Post> Detail(Guid id)
    {
        return await service.GetPostById(id);
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

        var theme = Request.Query["theme"].ToString();
        if (string.IsNullOrWhiteSpace(theme))
            theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
            theme = "default";

        // Get or create user
        var user = await userService.GetOrCreateUserAsync(username, theme);

        post.Username = user.Username;
        post.UserId = user.Id;
        post.Theme = theme;
        post.CreatedOnUrl = Request.Path.Value;

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

        await service.CreatePost(user.Id, user.Username, user.Theme, post.Message);

        UserWrite(username);

        await hubContext.Clients.All.SendAsync("SendMessage", post.Id, username, theme, post.Message, post.CreatedUtc);

        return NoContent();
    }
}
