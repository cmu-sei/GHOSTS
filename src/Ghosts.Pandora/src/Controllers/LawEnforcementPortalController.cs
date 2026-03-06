using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Pandora.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/law-enforcement")]
public class LawEnforcementPortalController(
    ILogger<LawEnforcementPortalController> logger,
    ILawEnforcementPortalService portalService)
    : BaseController(logger)
{
    private const string AdminUsername = "admin";
    private const string AdminPassword = "admin";
    private const string AuthCookieName = "law_enforcement_auth";

    private bool IsAuthenticated()
    {
        var authCookie = CookieRead(AuthCookieName);
        return authCookie == "authenticated";
    }

    private void SetAuthenticated()
    {
        var option = new CookieOptions
        {
            Expires = DateTime.Now.AddHours(8),
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        };
        Response.Cookies.Append(AuthCookieName, "authenticated", option);
    }

    private void ClearAuthentication()
    {
        Response.Cookies.Delete(AuthCookieName);
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Login");
        }

        return RedirectToAction("Dashboard");
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (IsAuthenticated())
        {
            return RedirectToAction("Dashboard");
        }

        return View("~/Views/LawEnforcementPortal/Login.cshtml");
    }

    [HttpPost("login")]
    public IActionResult Login([FromForm] LoginModel model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
        {
            ViewBag.Error = "Username and password are required.";
            return View("~/Views/LawEnforcementPortal/Login.cshtml");
        }

        if (model.Username.Trim() == AdminUsername && model.Password == AdminPassword)
        {
            SetAuthenticated();
            return RedirectToAction("Dashboard");
        }

        ViewBag.Error = "Invalid credentials. Please try again.";
        return View("~/Views/LawEnforcementPortal/Login.cshtml");
    }

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        ClearAuthentication();
        return RedirectToAction("Login");
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Login");
        }

        var requests = await portalService.GetAllRequestsAsync();
        return View("~/Views/LawEnforcementPortal/Dashboard.cshtml", requests);
    }

    [HttpGet("new-request")]
    public IActionResult NewRequest()
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Login");
        }

        return View("~/Views/LawEnforcementPortal/NewRequest.cshtml");
    }

    [HttpPost("new-request")]
    public async Task<IActionResult> CreateRequest([FromForm] RequestModel model)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Login");
        }

        if (model == null || string.IsNullOrWhiteSpace(model.RequestingAgency) ||
            string.IsNullOrWhiteSpace(model.CaseNumber) ||
            string.IsNullOrWhiteSpace(model.RequestType) ||
            string.IsNullOrWhiteSpace(model.Subject) ||
            string.IsNullOrWhiteSpace(model.Details))
        {
            ViewBag.Error = "All fields are required.";
            return View("~/Views/LawEnforcementPortal/NewRequest.cshtml");
        }

        await portalService.CreateRequestAsync(
            model.RequestingAgency,
            model.CaseNumber,
            model.RequestType,
            model.Subject,
            model.Details
        );

        ViewBag.Success = "Request submitted successfully!";
        return RedirectToAction("Dashboard");
    }

    [HttpGet("request/{id}")]
    public async Task<IActionResult> ViewRequest(int id)
    {
        if (!IsAuthenticated())
        {
            return RedirectToAction("Login");
        }

        var request = await portalService.GetRequestByIdAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        return View("~/Views/LawEnforcementPortal/ViewRequest.cshtml", request);
    }

    public class LoginModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class RequestModel
    {
        public string RequestingAgency { get; set; }
        public string CaseNumber { get; set; }
        public string RequestType { get; set; }
        public string Subject { get; set; }
        public string Details { get; set; }
    }
}
