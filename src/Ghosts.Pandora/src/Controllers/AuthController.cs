using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/login")]
public class AuthController(ILogger logger, IUserService userService, IThemeService themeService)
    : BaseController(logger)
{
    [HttpGet]
    public IActionResult Index(string returnUrl = "/")
    {
        ViewBag.Themes = themeService.GetAvailableThemes();
        ViewBag.SelectedTheme = string.IsNullOrWhiteSpace(ThemeRead()) ? "default" : ThemeRead();
        ViewBag.ReturnUrl = LocalReturnUrl(returnUrl);
        return View("~/Views/Auth/Login.cshtml");
    }

    /// <summary>
    /// Keeps a login bounce on this site, so a crafted returnUrl cannot hand the user off to
    /// an external origin.
    /// </summary>
    private string LocalReturnUrl(string returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginInputModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Username))
        {
            ViewBag.Error = "Username is required.";
            ViewBag.Themes = themeService.GetAvailableThemes();
            ViewBag.SelectedTheme = string.IsNullOrWhiteSpace(ThemeRead()) ? "default" : ThemeRead();
            ViewBag.ReturnUrl = LocalReturnUrl(model?.ReturnUrl);
            return View("~/Views/Auth/Login.cshtml");
        }

        var username = model.Username.Trim();

        var requestedTheme = string.IsNullOrWhiteSpace(model.Theme) ? ThemeRead() : model.Theme.Trim();
        if (string.IsNullOrWhiteSpace(requestedTheme) || !themeService.ThemeExists(requestedTheme))
        {
            requestedTheme = "default";
        }

        ThemeWrite(requestedTheme);

        await userService.GetOrCreateUserAsync(username, requestedTheme);

        UserWrite(username);

        return Redirect(LocalReturnUrl(model.ReturnUrl));
    }

    public class LoginInputModel
    {
        public string Username { get; set; }
        public string Theme { get; set; }
        public string ReturnUrl { get; set; }
    }
}
