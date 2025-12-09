using System.Net;
using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/directmessages")]
[SwaggerTag("API Functionality for DMs")]
public class DirectMessagesController(
    ILogger logger,
    ApplicationConfiguration applicationConfiguration,
    IDirectMessageService service,
    IUserService userService) : BaseController(logger)
{
    [SwaggerOperation(
        Summary = "Retrieve all DMs",
        Description = "Returns a list of all DMs.",
        OperationId = "GetDirectMessages")]
    [ProducesResponseType(typeof(IEnumerable<DirectMessage>), (int)HttpStatusCode.OK)]
    [HttpGet]
    public async Task<IEnumerable<DirectMessage>> Get()
    {
        return await service.GetAll(applicationConfiguration.DefaultDisplay);
    }

    [SwaggerOperation(
        Summary = "Retrieve all to|from DMs from an NPC",
        Description = "Returns a list of all to|from DMs from an NPC.",
        OperationId = "GetDirectMessagesById")]
    [ProducesResponseType(typeof(IEnumerable<DirectMessage>), (int)HttpStatusCode.OK)]
    [HttpGet("id/{id}")]
    public async Task<IEnumerable<DirectMessage>> GetById(Guid id)
    {
        return await service.GetReceivedMessagesAsync(id);
    }

    [SwaggerOperation(
        Summary = "Retrieve all to|from DMs from an NPC",
        Description = "Returns a list of all to|from DMs from an NPC.",
        OperationId = "GetDirectMessagesByUsername")]
    [ProducesResponseType(typeof(IEnumerable<DirectMessage>), (int)HttpStatusCode.OK)]
    [HttpGet("username/{username}")]
    public async Task<IEnumerable<DirectMessage>> GetByUsername(string username, string theme)
    {
        return await service.GetByUsername(username, theme, applicationConfiguration.DefaultDisplay);
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

        await service.CreateMessageAsync(model.FromUserId, model.ToUserId, model.Message);

        var directMessage = new DirectMessage
        {
            FromUserId = model.FromUserId,
            ToUserId = model.ToUserId,
            Message = model.Message,
            CreatedUtc = model.CreatedUtc == default ? DateTime.UtcNow : model.CreatedUtc
        };

        return Ok(directMessage);
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

        var from = await userService.GetOrCreateUserAsync(model.FromUserName, model.Theme);
        var to = await userService.GetOrCreateUserAsync(model.ToUserName, model.Theme);

        await service.CreateMessageAsync(from.Id, to.Id, model.Message);

        var directMessage = new DirectMessage
        {
            FromUserId = from.Id,
            ToUserId = to.Id,
            Message = model.Message,
            CreatedUtc = model.CreatedUtc == default ? DateTime.UtcNow : model.CreatedUtc
        };

        return Ok(directMessage);
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
