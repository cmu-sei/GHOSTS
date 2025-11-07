using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ghosts.Pandora.Controllers;

public class BaseController : Controller
{
    protected readonly ILogger Logger;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Ensure every request has an associated username and theme for downstream views
        ViewBag.Username = GetOrCreateUsernameCookie(context.HttpContext);

        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        ViewBag.Theme = theme;

        base.OnActionExecuting(context);
    }

    public override void OnActionExecuted(ActionExecutedContext filterContext)
    {
        var form = string.Empty;
        if (Request.HasFormContentType && Request.Form.Count != 0)
        {
            form = string.Join(",", Request.Form);
        }

        Logger.LogTrace("{RequestScheme}://{RequestHost}{RequestPath}{RequestQueryString}|{RequestMethod}|{Join}",
            Request.Scheme, Request.Host, Request.Path, Request.QueryString, Request.Method,
            form);

        ViewBag.Username = GetOrCreateUsernameCookie(filterContext.HttpContext);

        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        ViewBag.Theme = theme;
        base.OnActionExecuted(filterContext);
    }

    internal BaseController(ILogger logger)
    {
        Logger = logger;
    }

    internal void CookieWrite(string key, string value)
    {
        var option = new CookieOptions { Expires = DateTime.Now.AddMonths(1) };
        Response.Cookies.Append(key, value, option);
    }

    internal string CookieRead(string cookieName)
    {
        try
        {
            var c = Request.Cookies[cookieName];
            return c ?? string.Empty;
        }
        catch (Exception e)
        {
            Logger.LogDebug(e.Message);
            return string.Empty;
        }
    }

    internal string GetOrCreateUsernameCookie(HttpContext context, string fallbackUsername = null)
    {
        const string cookieName = "username";

        // Already exists?
        if (context.Request.Cookies.TryGetValue(cookieName, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // No cookie — create one using provided fallback (or generate something)
        var username = fallbackUsername ?? Faker.Internet.UserName();

        context.Response.Cookies.Append(
            cookieName,
            username,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddYears(5)
            });

        return username;
    }

    internal void UserWrite(string username)
    {
        CookieWrite("username", username);
    }

    internal string UserRead()
    {
        return CookieRead("username");
    }

    internal void ThemeWrite(string theme)
    {
        CookieWrite("theme", theme);
    }

    internal string ThemeRead()
    {
        return CookieRead("theme");
    }
}
