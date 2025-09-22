using Microsoft.AspNetCore.Mvc.Razor;

namespace Socializer.Infrastructure.Services;
public class ThemeViewLocationExpander : IViewLocationExpander
{
    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var http = context.ActionContext.HttpContext;
        var queryTheme = http.Request.Query["theme"].ToString();
        var cookieTheme = http.Request.Cookies["theme"];

        string theme;

        if (!string.IsNullOrEmpty(queryTheme))
        {
            // use query value and update cookie
            theme = queryTheme;
            http.Response.Cookies.Append("theme", theme, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        else
        {
            // fall back to cookie if available
            theme = cookieTheme ?? "default";
        }

        // normalize view name
        var view = context.ViewName.ToLowerInvariant();

        // these keys get merged into ExpandViewLocations()
        context.Values["theme"] = theme;
        context.Values["view"]  = view;

        Console.WriteLine($"theme={theme}, view={view}");
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
