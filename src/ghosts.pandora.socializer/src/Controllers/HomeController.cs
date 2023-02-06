using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

[Route("{*catchall}")]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IHubContext<PostsHub> _hubContext;
    private readonly DataContext _db;

    public HomeController(ILogger<HomeController> logger, IHubContext<PostsHub> hubContext, DataContext dbContext)
    {
        _logger = logger;
        _hubContext = hubContext;
        _db = dbContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        this._logger.LogTrace("{RequestScheme}://{RequestHost}{RequestPath}{RequestQueryString}|{RequestMethod}|", Request.Scheme, Request.Host, Request.Path, Request.QueryString, Request.Method);
        
        var posts = _db.Posts.Take(Program.Configuration.DefaultDisplay).ToList();

        if (Request.QueryString.HasValue && !string.IsNullOrEmpty(Request.Query["u"]))
            ViewBag.User = Request.Query["u"];
        return View(posts);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromForm] FilesController.FileInputModel model)
    {
        this._logger.LogTrace("{RequestScheme}://{RequestHost}{RequestPath}{RequestQueryString}|{RequestMethod}|{Join}", Request.Scheme, Request.Host, Request.Path, Request.QueryString, Request.Method, string.Join(",", Request.Form));
        
        var post = new Post
        {
            Id = Guid.NewGuid().ToString(),
            CreatedUtc = DateTime.UtcNow
        };
        
        var userFormValues = new [] {"user", "usr", "u", "uid", "user_id", "u_id"};
        foreach (var userFormValue in userFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[userFormValue])) continue;
            post.User = Request.Form[userFormValue]!;
            break;
        }
        
        var messageFormValues = new [] {"message", "msg", "m", "message_id", "msg_id", "msg_text", "text", "payload"};
        foreach (var messageFormValue in messageFormValues)
        {
            if (string.IsNullOrEmpty(Request.Form[messageFormValue])) continue;
            post.Message = Request.Form[messageFormValue]!;
            break;
        }
        
        if(string.IsNullOrEmpty(post.User) || string.IsNullOrEmpty(post.Message))
            return BadRequest("User and message are required.");

        // has the same user tried to post the same message within the past x minutes?
        if (_db.Posts.Any(_ => _.Message.ToLower() == post.Message.ToLower()
                                && _.User.ToLower() == post.User.ToLower()
                                && _.CreatedUtc > post.CreatedUtc.AddMinutes(-Program.Configuration.MinutesToCheckForDuplicatePost)))
        {
            this._logger.LogInformation("Client is posting duplicates: {PostUser}", post.User);
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
            post.Message = post.Message + $" <img src=\"{imagePath}\"/>";
        }
        
        _db.Posts.Add(post);
        await _db.SaveChangesAsync();
        
        await _hubContext.Clients.All.SendAsync("ReceiveMessage", post.Id, post.User, post.Message, post.CreatedUtc);

        return NoContent();
    }
}