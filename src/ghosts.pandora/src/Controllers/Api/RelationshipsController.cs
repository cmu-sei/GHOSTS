using Ghosts.Pandora.Infrastructure;
using Ghosts.Pandora.Infrastructure.Models;
using Ghosts.Pandora.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Pandora.Controllers.Api;

[Route("/api/relationships")]
[SwaggerTag("API Functionality for users.")]
public class RelationshipsController(
    ILogger logger,
    IFollowService followService,
    IUserService userService)
    : BaseController(logger)
{
    [HttpPost("follow")]
    public async Task<IActionResult> Follow([FromForm] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Target username is required.");
        }

        var follower = GetOrCreateUsernameCookie(HttpContext);
        if (string.IsNullOrWhiteSpace(follower))
        {
            return BadRequest("Unable to determine current user.");
        }

        var theme = ResolveThemeName();

        var followerUser = await userService.GetOrCreateUserAsync(follower, theme);
        var followeeUser = await userService.GetOrCreateUserAsync(username, theme);

        if (followerUser.Id == followeeUser.Id)
        {
            return BadRequest("You cannot follow yourself.");
        }

        var created = await followService.FollowAsync(followerUser.Id, followeeUser.Id);
        var followerCount = await followService.GetFollowerCountAsync(followeeUser.Id);

        return Ok(new
        {
            follower,
            followee = followeeUser.Username,
            success = created,
            followers = followerCount
        });
    }

    [HttpPost("unfollow")]
    public async Task<IActionResult> Unfollow([FromForm] string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Target username is required.");
        }

        var follower = GetOrCreateUsernameCookie(HttpContext);
        if (string.IsNullOrWhiteSpace(follower))
        {
            return BadRequest("Unable to determine current user.");
        }

        var theme = ResolveThemeName();
        var followerUser = await userService.GetUserByUsernameAsync(follower, theme);
        var followeeUser = await userService.GetUserByUsernameAsync(username, theme);

        if (followerUser == null || followeeUser == null)
        {
            return NotFound("User not found in this theme.");
        }

        if (followerUser.Id == followeeUser.Id)
        {
            return BadRequest("You cannot unfollow yourself.");
        }

        var removed = await followService.UnfollowAsync(followerUser.Id, followeeUser.Id);
        var followerCount = await followService.GetFollowerCountAsync(followeeUser.Id);

        return Ok(new
        {
            follower,
            followee = followeeUser.Username,
            success = removed,
            followers = followerCount
        });
    }

    [HttpGet("{username}/followers")]
    public async Task<IActionResult> GetFollowers(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var theme = ResolveThemeName();
        var user = await userService.GetUserByUsernameAsync(username, theme);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        var followers = await followService.GetFollowersAsync(user.Id);
        return Ok(new
        {
            username,
            total = followers.Count,
            followers = followers.Select(u => u.Username).ToArray()
        });
    }

    [HttpGet("{username}/following")]
    public async Task<IActionResult> GetFollowing(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var theme = ResolveThemeName();
        var user = await userService.GetUserByUsernameAsync(username, theme);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        var following = await followService.GetFollowingAsync(user.Id);
        return Ok(new
        {
            username,
            total = following.Count,
            following = following.Select(u => u.Username).ToArray()
        });
    }

    [HttpGet("{username}/connections")]
    public async Task<IActionResult> GetConnections(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var theme = ResolveThemeName();
        var user = await userService.GetUserByUsernameAsync(username, theme);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        var followers = await followService.GetFollowersAsync(user.Id, 200);
        var following = await followService.GetFollowingAsync(user.Id, 200);

        Func<User, object> projection = user => new
        {
            username = user.Username,
            displayName = user.Username,
            avatar = string.IsNullOrWhiteSpace(user.Avatar) ? $"/u/{user.Username}/avatar" : user.Avatar,
            theme = string.IsNullOrWhiteSpace(user.Theme) ? "default" : user.Theme,
            bio = user.Bio
        };

        return Ok(new
        {
            username,
            followers = followers.Select(projection).ToArray(),
            following = following.Select(projection).ToArray()
        });
    }

    private string ResolveThemeName()
    {
        var queryTheme = HttpContext.Request.Query["theme"].ToString();
        var theme = !string.IsNullOrWhiteSpace(queryTheme) ? queryTheme : ThemeRead();
        if (string.IsNullOrWhiteSpace(theme))
        {
            theme = "default";
        }

        ThemeWrite(theme);
        return theme;
    }
}
