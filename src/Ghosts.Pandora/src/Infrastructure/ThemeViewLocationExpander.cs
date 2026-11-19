using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Razor;

namespace Ghosts.Pandora.Infrastructure;
public class ThemeViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var http = context.ActionContext.HttpContext;
        var themes = http.RequestServices.GetService<IThemeService>();
        var queryTheme = Known(themes, http.Request.Query["theme"].ToString());
        var cookieTheme = Known(themes, http.Request.Cookies[CookieName(http)]);

        string theme;

        if (!string.IsNullOrEmpty(queryTheme))
        {
            // use query value and update cookie
            theme = queryTheme;
            http.Response.Cookies.Append(CookieName(http), theme, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        else
        {
            // fall back to cookie, then to the configured default theme
            var defaultTheme = Known(themes, http.RequestServices.GetService<ApplicationConfiguration>()?.Mode.DefaultTheme);
            theme = cookieTheme ?? defaultTheme ?? "default";
        }

        // normalize view name
        var view = context.ViewName.ToLowerInvariant();

        // these keys get merged into ExpandViewLocations()
        context.Values["theme"] = theme;
        context.Values["view"]  = view;

        Console.WriteLine($"theme={theme}, view={view}");
    }

    /// <summary>
    /// The theme cookie's name. Browsers share cookies between ports of one host, so sites told apart
    /// only by port (several Pandora sites on localhost under Aspire) each keep their own; a site on
    /// its own hostname keeps the plain "theme" cookie.
    /// </summary>
    public static string CookieName(HttpContext http) =>
        http.Request.Host.Port is { } port ? $"theme-{port}" : "theme";

    /// <summary>The theme in lower case when the app has it, else null, so no other value becomes part of a view path.</summary>
    private static string Known(IThemeService themes, string theme)
    {
        var name = theme?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(name) && themes?.ThemeExists(name) != false ? name : null;
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext ctx,
        IEnumerable<string> viewLocations)
    {
        if (ctx.Values.TryGetValue("theme", out var theme) &&
            ctx.Values.TryGetValue("view", out var view))
        {
            yield return $"/Views/Themes/{theme}/{view}.cshtml";
        }

        foreach (var loc in viewLocations)
            yield return loc;
    }
}
