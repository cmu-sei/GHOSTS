using Ghosts.Socializer.Infrastructure.Services;
using Ghosts.Socializer.Infrastructure.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Socializer.Controllers;

[ApiExplorerSettings(IgnoreApi = true)]
[Route("/search")]
public class SearchController(ILogger logger, IUserService userService, IPostService postService)
    : BaseController(logger)
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string q)
    {
        var theme = ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        var viewModel = new SearchResultsViewModel
        {
            Query = q,
            Theme = theme
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            viewModel.Users = await userService.SearchUsersAsync(q, limit: 50);
            viewModel.Posts = await postService.SearchPostsAsync(q, theme, limit: 50);
        }

        ViewBag.Theme = theme;
        return View($"~/Views/Themes/{theme}/search.cshtml", viewModel);
    }
}
