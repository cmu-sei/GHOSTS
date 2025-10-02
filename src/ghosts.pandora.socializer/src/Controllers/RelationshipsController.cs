using System;
using System.Linq;
using Ghosts.Socializer.Infrastructure;
using Ghosts.Socializer.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Socializer.Controllers;

[ApiController]
public class RelationshipsController(
    ILogger logger,
    IFollowService followService,
    IUserService userService)
    : BaseController(logger)
{
    [HttpPost("/follow")]
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

        if (string.Equals(follower, username, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot follow yourself.");
        }

        await userService.GetOrCreateUserAsync(follower);
        await userService.GetOrCreateUserAsync(username);

        var created = await followService.FollowAsync(follower, username);
        var followerCount = await followService.GetFollowerCountAsync(username);

        return Ok(new
        {
            follower,
            followee = username,
            success = created,
            followers = followerCount
        });
    }

    [HttpPost("/unfollow")]
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

        if (string.Equals(follower, username, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot unfollow yourself.");
        }

        var removed = await followService.UnfollowAsync(follower, username);
        var followerCount = await followService.GetFollowerCountAsync(username);

        return Ok(new
        {
            follower,
            followee = username,
            success = removed,
            followers = followerCount
        });
    }

    [HttpGet("/followers/{username}")]
    public async Task<IActionResult> GetFollowers(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var followers = await followService.GetFollowersAsync(username);
        return Ok(new
        {
            username,
            total = followers.Count,
            followers = followers.Select(u => u.Username).ToArray()
        });
    }

    [HttpGet("/following/{username}")]
    public async Task<IActionResult> GetFollowing(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var following = await followService.GetFollowingAsync(username);
        return Ok(new
        {
            username,
            total = following.Count,
            following = following.Select(u => u.Username).ToArray()
        });
    }

    [HttpGet("/api/users/{username}/connections")]
    public async Task<IActionResult> GetConnections(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest("Username is required.");
        }

        var followers = await followService.GetFollowersAsync(username, 200);
        var following = await followService.GetFollowingAsync(username, 200);

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
}
