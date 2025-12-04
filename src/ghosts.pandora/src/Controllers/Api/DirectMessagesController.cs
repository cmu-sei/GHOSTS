using System.Net;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/directmessages")]
[SwaggerTag("API Functionality for DMs")]
public class DirectMessagesController(
    ILogger logger,
    DataContext dbContext,
    ApplicationConfiguration applicationConfiguration) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Retrieve all DMs",
        Description = "Returns a list of all DMs.",
        OperationId = "GetDirectMessages")]
    [ProducesResponseType(typeof(IEnumerable<DirectMessage>), (int)HttpStatusCode.OK)]
    [HttpGet]
    public IEnumerable<DirectMessage> Get()
    {
        var list = dbContext.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        return list;
    }

    [SwaggerOperation(
        Summary = "Retrieve all to|from DMs from an NPC",
        Description = "Returns a list of all to|from DMs from an NPC.",
        OperationId = "GetDirectMessagesById")]
    [ProducesResponseType(typeof(IEnumerable<DirectMessage>), (int)HttpStatusCode.OK)]
    [HttpGet("id/{id}")]
    public IEnumerable<DirectMessage> GetById(Guid id)
    {
        List<DirectMessage> list;

        list = dbContext.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .Where(x=>x.FromUser.Id == id || x.ToUser.Id == id)
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        return list;
    }

    [SwaggerOperation(
        Summary = "Retrieve all to|from DMs from an NPC",
        Description = "Returns a list of all to|from DMs from an NPC.",
        OperationId = "GetDirectMessagesByUsername")]
    [ProducesResponseType(typeof(IEnumerable<DirectMessage>), (int)HttpStatusCode.OK)]
    [HttpGet("username/{username}")]
    public IEnumerable<DirectMessage> GetByUsername(string username, string theme)
    {
        List<DirectMessage> list;

        list = dbContext.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .Where(x=> (x.FromUser.Username == username && x.FromUser.Theme == theme)
                       || (x.ToUser.Username == username && x.ToUser.Theme == theme))
            .OrderByDescending(x => x.CreatedUtc)
            .Take(applicationConfiguration.DefaultDisplay)
            .ToList();

        return list;
    }

    [SwaggerOperation(
        Summary = "Creates a direct message by user Ids",
        Description = "Creates a direct message by user Ids",
        OperationId = "PostDirectMessageByUserIdViewModel")]
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [HttpPost("id")]
    public async Task<IActionResult> Post([FromForm] DirectMessageByUserIdViewModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Message))
        {
            return BadRequest("A message payload is required.");
        }

        var directMessage = new DirectMessage
        {
            FromUserId = model.FromUserId,
            ToUserId = model.ToUserId,
            Message = model.Message,
            CreatedUtc = model.CreatedUtc == default ? DateTime.UtcNow : model.CreatedUtc
        };

        var fromExists = await dbContext.Users.AnyAsync(u => u.Id == directMessage.FromUserId);
        var toExists = await dbContext.Users.AnyAsync(u => u.Id == directMessage.ToUserId);

        if (!fromExists || !toExists)
        {
            return BadRequest("Valid to and from users are required.");
        }

        await dbContext.DirectMessages.AddAsync(directMessage);
        await dbContext.SaveChangesAsync();
        var persisted = await dbContext.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .FirstOrDefaultAsync(dm => dm.Id == directMessage.Id);

        return Ok(persisted ?? directMessage);
    }

    [SwaggerOperation(
        Summary = "Creates a direct message by usernames and theme",
        Description = "Creates a direct message by usernames and theme",
        OperationId = "PostDirectMessageByUsernameViewModel")]
    [ProducesResponseType(typeof(IActionResult), (int)HttpStatusCode.OK)]
    [HttpPost("username")]
    public async Task<IActionResult> Post([FromForm] DirectMessageByUsernameViewModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Message))
        {
            return BadRequest("A message payload is required.");
        }

        // get users by theme
        var from = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == model.FromUserName && x.Theme == model.Theme);
        var to = await dbContext.Users.FirstOrDefaultAsync(x => x.Username == model.ToUserName && x.Theme == model.Theme);

        if (from == null)
        {
            var user = new User { Username = model.FromUserName, Theme = model.Theme, CreatedUtc = DateTime.UtcNow };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();
            from = user;
        }

        if (to == null)
        {
            var user = new User { Username = model.ToUserName, Theme = model.Theme, CreatedUtc = DateTime.UtcNow };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();
            to = user;
        }

        var directMessage = new DirectMessage
        {
            FromUserId = from.Id,
            ToUserId = to.Id,
            Message = model.Message,
            CreatedUtc = model.CreatedUtc == default ? DateTime.UtcNow : model.CreatedUtc
        };

        await dbContext.DirectMessages.AddAsync(directMessage);
        await dbContext.SaveChangesAsync();
        var persisted = await dbContext.DirectMessages
            .Include(dm => dm.FromUser)
            .Include(dm => dm.ToUser)
            .FirstOrDefaultAsync(dm => dm.Id == directMessage.Id);

        return Ok(persisted ?? directMessage);
    }

    public class DirectMessageByUsernameViewModel
    {
        public string FromUserName { get; set; }
        public string ToUserName { get; set; }
        public string Theme { get; set; }
        public string Message { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    public class DirectMessageByUserIdViewModel
    {
        public Guid FromUserId { get; set; }
        public Guid ToUserId { get; set; }
        public string Message { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
