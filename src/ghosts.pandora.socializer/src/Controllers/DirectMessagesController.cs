using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Socializer.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/messages")]
public class DirectMessagesController(
    ILogger logger, IDirectMessageService directMessageService, IUserService userService)
    : BaseController(logger)
{
    // Main messages route - theme determined by cookie
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var user = await userService.GetOrCreateUserAsync(username);
        var themeName = await ResolveThemeAsync(user);

        var receivedMessages = await directMessageService.GetReceivedMessagesAsync(username);
        var sentMessages = await directMessageService.GetSentMessagesAsync(username);

        ViewBag.Theme = themeName;
        ViewBag.Username = username;
        ViewBag.ReceivedMessages = receivedMessages;
        ViewBag.SentMessages = sentMessages;
        ViewBag.ConversationPartners = await directMessageService.GetConversationPartnersAsync(username);

        return View($"~/Views/Themes/{themeName}/messages.cshtml");
    }

    [HttpGet("conversation/{partnerUsername}")]
    public async Task<IActionResult> Conversation(string partnerUsername)
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var user = await userService.GetOrCreateUserAsync(username);
        var themeName = await ResolveThemeAsync(user);

        var conversation = await directMessageService.GetConversationAsync(username, partnerUsername);

        // Mark messages as read
        foreach (var message in conversation.Where(m => m.ToUsername == username && m.ReadUtc == null))
        {
            await directMessageService.MarkAsReadAsync(message.Id);
        }

        ViewBag.Theme = themeName;
        ViewBag.Username = username;
        ViewBag.PartnerUsername = partnerUsername;
        ViewBag.Conversation = conversation;

        return View($"~/Views/Themes/{themeName}/conversation.cshtml");
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendMessage(string toUsername, string message)
    {
        var fromUsername = GetOrCreateUsernameCookie(this.HttpContext);

        await userService.CreateUserAsync(fromUsername);
        await userService.CreateUserAsync(toUsername);

        await directMessageService.SendMessageAsync(fromUsername, toUsername, message);

        return RedirectToAction("Conversation", new { partnerUsername = toUsername });
    }

    [HttpGet("/api/messages/unread")]
    public async Task<IActionResult> GetUnreadMessages()
    {
        var username = GetOrCreateUsernameCookie(this.HttpContext);
        var unreadMessages = await directMessageService.GetUnreadMessagesAsync(username);
        return Ok(unreadMessages);
    }

    [HttpPost("/api/messages/{messageId:int}/read")]
    public async Task<IActionResult> MarkAsRead(int messageId)
    {
        await directMessageService.MarkAsReadAsync(messageId);
        return NoContent();
    }

    private async Task<string> ResolveThemeAsync(User user)
    {
        var cookieTheme = ThemeRead();
        var selectedTheme = !string.IsNullOrWhiteSpace(cookieTheme)
            ? cookieTheme
            : user?.Theme;

        if (string.IsNullOrWhiteSpace(selectedTheme))
        {
            selectedTheme = "default";
        }

        if (user != null && !string.Equals(user.Theme, selectedTheme, StringComparison.OrdinalIgnoreCase))
        {
            await userService.UpdateUserAsync(user.Username, theme: selectedTheme);
            user.Theme = selectedTheme;
        }

        ThemeWrite(selectedTheme);
        return selectedTheme;
    }
}
