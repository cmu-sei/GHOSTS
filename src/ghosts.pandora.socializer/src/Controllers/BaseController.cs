using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using Socializer.Hubs;
using Socializer.Infrastructure;

namespace Socializer.Controllers;

public class BaseController : Controller
{
    protected readonly ILogger Logger;
    protected readonly IHubContext<PostsHub> HubContext;
    protected readonly DataContext Db;

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

        ViewBag.UserId = CookieRead("userid");
        base.OnActionExecuted(filterContext);
    }

    internal BaseController(ILogger logger, IHubContext<PostsHub> hubContext, DataContext dbContext)
    {
        Logger = logger;
        HubContext = hubContext;
        Db = dbContext;
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
}
