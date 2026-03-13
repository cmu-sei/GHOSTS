using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/messages")]
public class DirectMessagesController(
    ILogger logger, IDirectMessageService directMessageService,
    IUserService userService, ApplicationConfiguration applicationConfiguration)
    : BaseController(logger)
{
    // Main messages route - theme determined by cookie
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery(Name = "workflowId")] string workflowId = "5e56a263-3a40-44bd-bc9d-1cfb3bc2a87d")
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var themeName = ResolveThemeName();
        var user = await userService.GetOrCreateUserAsync(username, themeName);

        var receivedMessages = await directMessageService.GetReceivedMessagesAsync(user.Id);
        var sentMessages = await directMessageService.GetSentMessagesAsync(user.Id);

        ViewBag.WorkflowId = workflowId;
        ViewBag.Theme = themeName;
        ViewBag.Username = username;
        ViewBag.ReceivedMessages = receivedMessages;
        ViewBag.SentMessages = sentMessages;
        var partners = await directMessageService.GetConversationPartnersAsync(user.Id);
        ViewBag.ConversationPartners = partners.Select(p => p.Username).ToArray();

        return View($"~/Views/Themes/{themeName}/messages.cshtml");
    }

    [HttpGet("conversation/{partnerUsername}")]
    public async Task<IActionResult> Conversation(string partnerUsername)
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var themeName = ResolveThemeName();
        var user = await userService.GetOrCreateUserAsync(username, themeName);
        var partner = await userService.GetOrCreateUserAsync(partnerUsername, themeName);

        var conversation = await directMessageService.GetConversationAsync(user.Id, partner.Id);

        // Mark messages as read
        foreach (var message in conversation.Where(m => m.ToUserId == user.Id && m.ReadUtc == null))
        {
            await directMessageService.MarkAsReadAsync(message.Id);
        }

        ViewBag.Theme = themeName;
        ViewBag.Username = username;
        ViewBag.PartnerUsername = partner.Username;
        ViewBag.Conversation = conversation;

        return View($"~/Views/Themes/{themeName}/conversation.cshtml");
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage(string toUsername, string message, string fromUsername, string theme)
    {
        if(string.IsNullOrEmpty(fromUsername))
            fromUsername = GetOrCreateUsernameCookie(this.HttpContext);
        if(string.IsNullOrEmpty(theme))
            theme = ResolveThemeName();

        var fromUser = await userService.GetOrCreateUserAsync(fromUsername, theme);
        var toUser = await userService.GetOrCreateUserAsync(toUsername, theme);

        await directMessageService.CreateMessageAsync(fromUser.Id, toUser.Id, message);

        return RedirectToAction("Conversation", new { partnerUsername = toUsername });
    }

    [HttpGet("unread")]
    public async Task<IActionResult> GetUnreadMessages()
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var themeName = ResolveThemeName();
        var user = await userService.GetOrCreateUserAsync(username, themeName);
        var unreadMessages = await directMessageService.GetUnreadMessagesAsync(user.Id);
        return Ok(unreadMessages);
    }

    [HttpGet("chat")]
    public async Task<IActionResult> RealTimeChat(
        [FromQuery(Name = "workflowId")] string workflowId = "5e56a263-3a40-44bd-bc9d-1cfb3bc2a87d",
        [FromQuery(Name = "fromUsername")] string from = "",
        [FromQuery(Name = "toUsername")] string to = "")
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var themeName = ResolveThemeName();

        ViewBag.Theme = themeName;
        ViewBag.Username = username;
        ViewBag.WorkflowId = workflowId;
        ViewBag.From = from;
        ViewBag.To = to;
        ViewBag.WorkflowsUrl = applicationConfiguration.Ghosts.WorkflowsUrl;

        return View($"~/Views/Themes/{themeName}/chat.cshtml");
    }

    [HttpPost("{messageId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int messageId)
    {
        await directMessageService.MarkAsReadAsync(messageId);
        return NoContent();
    }

    private string ResolveThemeName()
    {
        var cookieTheme = ThemeRead();
        var queryTheme = HttpContext.Request.Query["theme"].ToString();
        var selectedTheme = !string.IsNullOrWhiteSpace(queryTheme)
            ? queryTheme
            : cookieTheme;

        if (string.IsNullOrWhiteSpace(selectedTheme))
        {
            selectedTheme = "default";
        }

        ThemeWrite(selectedTheme);
        return selectedTheme;
    }
}
