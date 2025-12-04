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
        ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl;
        return View("~/Views/Auth/Login.cshtml");
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginInputModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Username))
        {
            ViewBag.Error = "Username is required.";
            ViewBag.Themes = themeService.GetAvailableThemes();
            ViewBag.SelectedTheme = string.IsNullOrWhiteSpace(ThemeRead()) ? "default" : ThemeRead();
            ViewBag.ReturnUrl = string.IsNullOrWhiteSpace(model?.ReturnUrl) ? "/" : model!.ReturnUrl;
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

        return Redirect(string.IsNullOrWhiteSpace(model.ReturnUrl) ? "/" : model.ReturnUrl);
    }

    public class LoginInputModel
    {
        public string Username { get; set; }
        public string Theme { get; set; }
        public string ReturnUrl { get; set; }
    }
}
