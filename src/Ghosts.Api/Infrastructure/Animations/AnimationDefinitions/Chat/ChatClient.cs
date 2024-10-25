// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Hubs;
using ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat.Mattermost;
using ghosts.api.Infrastructure.ContentServices;
using ghosts.api.Infrastructure.Extensions;
using ghosts.api.Infrastructure.Models;
using Ghosts.Animator.Extensions;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Extensions;
using Ghosts.Domain.Code.Helpers;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat;

public class ChatClient
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ChatJobConfiguration _configuration;
    private readonly ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.ChatSettings _chatSettings;
    private string _baseUrl;
    private readonly HttpClient _client;
    private string _token;
    private string UserId { get; set; }
    private readonly IFormatterService _formatterService;

    private readonly IHubContext<ActivityHub> _activityHubContext;
    private readonly CancellationToken _cancellationToken;

    public ChatClient(ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.ChatSettings chatSettings,
        ChatJobConfiguration config, IFormatterService formatterService, IHubContext<ActivityHub> activityHubContext,
        CancellationToken ct)
    {
        _configuration = config;
        _chatSettings = chatSettings;
        _baseUrl = _chatSettings.PostUrl;
        _client = new HttpClient();
        _formatterService = formatterService;
        _activityHubContext = activityHubContext;
        _cancellationToken = ct;
    }

    private async Task<User> AdminLogin()
    {
        try
        {
            return await Login(_configuration.Chat.AdminUsername,
                _configuration.Chat.AdminPassword);
        }
        catch (Exception e)
        {
            _log.Error($"Cannot login the configured admin account – {_configuration.Chat.AdminUsername} with error {e.Message}|{e.StackTrace}");
            return null;
        }
    }

    private async Task<User> Login(string username, string password)
    {
        _baseUrl = _baseUrl.TrimEnd('/');
        var url = $"{_baseUrl}/api/v4/users/login";
        _log.Trace($"Using login url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var content = new StringContent($"{{\"login_id\":\"{username}\",\"password\":\"{password}\"}}", null,
                "application/json");
            request.Content = content;
            var response = await _client.SendAsync(request, _cancellationToken);
            response.EnsureSuccessStatusCode();

            // Reading the 'Token' value from response headers
            if (response.Headers.TryGetValues("Token", out var values))
            {
                _token = values.First(); // Assuming there's at least one value
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }
            else
            {
                throw new Exception("Token not found in the response headers.");
            }

            var contentString = await response.Content.ReadAsStringAsync(_cancellationToken);
            var user = JsonSerializer.Deserialize<User>(contentString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new Exception("A user was not returned properly");
            UserId = user.Id;
            return user;
        }
        catch (Exception e)
        {
            _log.Error($"Cannot login {username}:{password} with error {e.Message}|{e.StackTrace}");
            throw;
        }
    }

    private async Task<IEnumerable<Team>> GetMyTeams(User user)
    {
        var url = $"{_baseUrl}/api/v4/users/{user.Id}/teams";
        _log.Trace($"Using get teams url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {
                var response = JsonSerializer.Deserialize<IEnumerable<Team>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return response;
            }
        }
        catch (Exception e)
        {
            _log.Error($"No teams found {e}");
        }

        return new List<Team>();
    }

    private async Task<IEnumerable<Channel>> GetMyChannels(User user)
    {
        var url = $"{_baseUrl}/api/v4/users/{user.Id}/channels";
        _log.Trace($"Using get channels url: {url}");

        var content = string.Empty;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {
                if (!content.Contains("detailed_error"))
                {
                    var response = JsonSerializer.Deserialize<IEnumerable<Channel>>(content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return response;
                }
            }
        }
        catch (Exception e)
        {
            _log.Trace($"No channels found in {content} {e}");
        }

        return new List<Channel>();
    }

    private async Task CreateUser(UserCreate create)
    {
        var url = $"{_baseUrl}/api/v4/users";
        _log.Trace($"Using create user url: {url}");

        try
        {
            var jsonPayload = JsonSerializer.Serialize(create.ToObject());
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            await ExecuteRequest(request);
        }
        catch (Exception e)
        {
            _log.Error($"Create user failed {e}");
        }
    }

    private async Task<IEnumerable<User>> GetUsers()
    {
        var url = $"{_baseUrl}/api/v4/users";
        _log.Trace($"Using get users url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {
                var response = JsonSerializer.Deserialize<IEnumerable<User>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return response;
            }
        }
        catch (Exception e)
        {
            _log.Error($"Get users failed {e}");
        }

        return new List<User>();
    }

    private async Task<User> GetUserByUsername(string username)
    {
        var url = $"{_baseUrl}/api/v4/users/username/{username}";
        _log.Trace($"Using get user by username url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {
                var response = JsonSerializer.Deserialize<User>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return response;
            }
        }
        catch (Exception e)
        {
            _log.Error($"Get user by username failed {e}");
        }

        return new User();
    }

    private async Task<User> GetUserById(string id)
    {
        var url = $"{_baseUrl}/api/v4/users/{id}";
        _log.Trace($"Using get user by id url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {
                var response = JsonSerializer.Deserialize<User>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return response;
            }
        }
        catch (Exception e)
        {
            _log.Error($"Get user by id failed {e}");
        }

        return new User();
    }

    private async Task JoinTeam(string userId, string teamId)
    {
        var payload = new
        {
            team_id = teamId,
            user_id = userId
        };

        var url = $"{_baseUrl}/api/v4/teams/{teamId}/members";
        _log.Trace($"Using join team url: {url}");

        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            await ExecuteRequest(request);
        }
        catch (Exception e)
        {
            _log.Error($"Join team failed {e}");
        }
    }

    private async Task JoinChannel(string userId, string channelId)
    {
        var payload = new
        {
            user_id = userId
        };

        var url = $"{_baseUrl}/api/v4/channels/{channelId}/members";
        _log.Trace($"Using join channel url: {url}");

        try
        {
            var jsonPayload = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            await ExecuteRequest(request);
        }
        catch (Exception e)
        {
            _log.Error($"Join channel failed {e}");
        }
    }

    private async Task<IEnumerable<Team>> GetTeams()
    {
        var url = $"{_baseUrl}/api/v4/teams";
        _log.Trace($"Using get teams url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            var response = JsonSerializer.Deserialize<IEnumerable<Team>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return response;
        }
        catch (Exception e)
        {
            _log.Error($"Get teams failed {e}");
            return new List<Team>();
        }
    }

    private async Task<IEnumerable<Channel>> GetChannelsByTeam(string teamId)
    {
        var url = $"{_baseUrl}/api/v4/teams/{teamId}/channels";
        _log.Trace($"Using get channels url: {url}");

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {

                var response = JsonSerializer.Deserialize<IEnumerable<Channel>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return response;
            }
        }
        catch (Exception e)
        {
            _log.Error($"No channels found {e}");
        }

        return new List<Channel>();
    }

    // public async Task GetUnreadPosts()
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}users/me/teams/unread");
    //     var content = await ExecuteRequest(request);
    //     //TODO
    // }
    //
    // public async Task GetUnreadPostsByTeam(string teamId)
    // {
    //     var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}users/me/teams/{teamId}/unread");
    //     var content = await ExecuteRequest(request);
    //     //TODO
    // }

    private async Task<PostResponse> GetPostsByChannel(string channelId, string afterPostId = "")
    {
        var url = $"{_baseUrl}/api/v4/channels/{channelId}/posts?after={afterPostId}";
        _log.Trace($"Using get posts by channel url: {url}");

        try
        {
            var request =
                new HttpRequestMessage(HttpMethod.Get, url);
            var content = await ExecuteRequest(request);

            if (!string.IsNullOrEmpty(content))
            {
                var response = JsonSerializer.Deserialize<PostResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return response;
            }
        }
        catch (Exception e)
        {
            _log.Error($"No posts found {e}");
        }

        return new PostResponse();
    }


    private async Task<Post> CreatePost(string channelId, string m)
    {
        if (!string.IsNullOrEmpty(channelId))
        {
            // JSON payload
            var payload = new
            {
                channel_id = channelId,
                message = m
            };

            var url = $"{_baseUrl}/api/v4/posts";
            _log.Trace($"Using create post url: {url}");

            // Serialize the payload to JSON
            var jsonPayload = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            var content = await ExecuteRequest(request);
            if (string.IsNullOrEmpty(content))
            {
                _log.Trace("content was empty!");
            }
            else
            {
                try
                {
                    var response =
                        JsonSerializer.Deserialize<Post>(content,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return response;
                }
                catch (Exception e)
                {
                    _log.Error(e);
                }
            }
        }

        return new Post();
    }

    private async Task<string> ExecuteRequest(HttpRequestMessage request)
    {
        try
        {
            var response = await _client.SendAsync(request, _cancellationToken);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
            {
                _log.Info(await response.Content.ReadAsStringAsync(_cancellationToken));
                return string.Empty;
            }

            response.EnsureSuccessStatusCode();
            var contentString = await response.Content.ReadAsStringAsync(_cancellationToken);

            return contentString;
        }
        catch (Exception e)
        {
            _log.Error(request.RequestUri != null
                ? $"Error with {request.Method} request {request.RequestUri} : {e}"
                : $"Error with {request.Method} request to null uri : {e}");
            return string.Empty;
        }
    }

    public async Task Step(Random random, IEnumerable<NpcRecord> agents)
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            if (agents == null || !agents.Any())
                return;

            var u = await AdminLogin();
            if (u == null)
            {
                // Log failure for admin login
                Console.WriteLine("Admin login failed. Cannot proceed with agent steps.");
                return;
            }

            var agentsWithAccounts = await GetUsers();
            var agentsWithAccountsHash = new HashSet<string>(agentsWithAccounts.Select(a => a.Email));
            var agentList = agents.ToList();

            foreach (var agent in agentList)
            {
                var username = agent.NpcProfile.Email.CreateUsernameFromEmail();
                if (!agentsWithAccountsHash.Contains(agent.NpcProfile.Email))
                {
                    try
                    {
                        await CreateUser(new UserCreate
                        {
                            Email = agent.NpcProfile.Email,
                            FirstName = agent.NpcProfile.Name.First,
                            LastName = agent.NpcProfile.Name.Last,
                            Nickname = agent.NpcProfile.Name.ToString() ?? string.Empty,
                            Password = _configuration.Chat.DefaultUserPassword,
                            Username = username
                        });

                        Console.WriteLine($"User created: {username}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating user {username}: {ex.Message}");
                        continue; // Skip to the next agent if user creation fails
                    }
                }

                try
                {
                    await StepEx(random, agent.Id, username, _configuration.Chat.DefaultUserPassword);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing step for {username}: {ex.Message}");
                    // Optionally, you could log the error but continue with the other agents
                }
            }
        }
    }

    private async Task StepEx(Random random, Guid npcId, string username, string password)
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            _log.Trace($"Managing {username}...");

            if (username.Contains("admin"))
            {
                _log.Trace($"Skipping user {username}...");
                return;
            }

            try
            {
                await Login(username, password);
                _log.Trace($"{username} is now logged in");
            }
            catch (Exception e)
            {
                _log.Warn($"Could not login {username}, {password} with: {e}");
                return;
            }

            var me = await GetUserByUsername(username);
            var channelHistory = new List<ChannelHistory>();
            try
            {
                var numberOfPosts = _chatSettings.PostProbabilities.GetWeightedRandomProbabilityResult();
                _log.Trace($"Preparing to post {numberOfPosts} times for {username}");
                for (var i = 0; i <= numberOfPosts; i++)
                {
                    var myTeams = await GetMyTeams(me);
                    var myChannels = await GetMyChannels(me);
                    var myChannelsList = myChannels as Channel[] ?? myChannels.ToArray();

                    var teams = await GetTeams();
                    var teamsList = teams as Team[] ?? teams.ToArray();
                    var notMyTeams = teamsList.Except(myTeams, new TeamComparer()).ToList();

                    // Do something with the teams not in 'myTeams'
                    foreach (var team in notMyTeams.Where(x => x.AllowOpenInvite is true))
                    {
                        await JoinTeam(UserId, team.Id);
                    }

                    foreach (var team in teamsList)
                    {
                        _log.Trace($"{UserId} TEAM {team.Name}");
                        var channels = await GetChannelsByTeam(team.Id);
                        var channelsList = channels as Channel[] ?? channels.ToArray();
                        var notMyChannels = channelsList.Except(myChannelsList, new ChannelComparer()).ToList();

                        foreach (var channel in notMyChannels)
                        {
                            await JoinChannel(UserId, channel.Id);
                        }

                        foreach (var channel in channelsList)
                        {
                            _log.Trace($"{UserId} CHANNEL: {channel.Id}, {channel.Name}");
                            var lastPost = channelHistory.OrderByDescending(x => x.Created)
                                .FirstOrDefault(x => x.ChannelId == channel.Id);
                            var postId = string.Empty;
                            if (lastPost != null)
                                postId = lastPost.PostId;
                            var posts = await GetPostsByChannel(channel.Id, postId);
                            try
                            {
                                if (posts?.Posts != null)
                                {
                                    foreach (var post in posts.Posts.Where(x => x.Value?.Type == ""))
                                    {
                                        var user = await GetUserById(post.Value.UserId);
                                        if (user == null)
                                        {
                                            const string email = "some.one@user.com";
                                            user = new User
                                            {
                                                FirstName = "some",
                                                LastName = "one",
                                                Email = email,
                                                Username = email.CreateUsernameFromEmail()
                                            };
                                        }

                                        channelHistory.Add(new ChannelHistory
                                        {
                                            ChannelId = channel.Id,
                                            ChannelName = channel.Name,
                                            UserId = post.Value.Id,
                                            PostId = post.Value.Id,
                                            UserName = user.Username,
                                            Created = post.Value.CreateAt.ToDateTime(),
                                            Message = post.Value.Message
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Trace($"An error occurred: {ex.Message}");
                            }
                        }
                    }

                    var subPrompt = _configuration.Prompts.GetRandom(random);
                    _log.Trace($"{username} looking at posts...");

                    var randomChannelToPostTo = channelHistory.Select(x => x.ChannelId).ToArray().GetRandom(random);
                    if (string.IsNullOrEmpty(randomChannelToPostTo))
                    {
                        if (myChannelsList.Any())
                        {
                            randomChannelToPostTo = myChannelsList.PickRandom().Id;
                        }
                        else
                        {
                            _log.Trace($"User somehow has no channels. Is server configured correctly? {username}");
                            continue;
                        }
                    }

                    var history = channelHistory
                        .Where(x => x.ChannelId == randomChannelToPostTo && x.UserName != me.Username)
                        .MaxBy(x => x.Created);
                    //var historyString = history is { Message.Length: >= 100 } ? history.Message[..100] : history?.Message;
                    var historyString = string.Empty;
                    if (history != null)
                        historyString = history.Message;

                    var prompt = $"Write my update to the chat system that {subPrompt}";
                    var respondingTo = string.Empty;
                    if (random.Next(0, 99) < _chatSettings.PercentReplyVsNew && !string.IsNullOrEmpty(historyString) &&
                        history.UserId != me.Id)
                    {
                        prompt =
                            $"How do I respond to this? {historyString}";
                        respondingTo = history.UserName;
                    }

                    var message = await _formatterService.ExecuteQuery(prompt);
                    message = message.Clean(_configuration.Replacements, random);
                    if (string.IsNullOrEmpty(message))
                    {
                        _log.Trace($"Empty message for {me.Username} {prompt}. Continuing..");
                        return;
                    }

                    if (!string.IsNullOrEmpty(respondingTo))
                    {
                        var f = $"{historyString} | {respondingTo} | {message}";
                        message =
                            $"> {historyString.Replace(">", "")}{Environment.NewLine}{Environment.NewLine}@{respondingTo} {message}";
                        _log.Trace($"{historyString} | {respondingTo} | {message}{Environment.NewLine}{f}");
                    }

                    if (message.ShouldSend(_configuration.Drops))
                    {
                        if (username.Contains("admin"))
                            return;

                        var post = await CreatePost(randomChannelToPostTo, message);
                        _log.Info($"{prompt}|SENT|{post.Message}");


                        //post to hub
                        await _activityHubContext.Clients.All.SendAsync("show",
                            1,
                            npcId,
                            "chat",
                            message,
                            DateTime.Now.ToString(CultureInfo.InvariantCulture), _cancellationToken
                        );
                    }
                    else
                    {
                        _log.Info($"{prompt}|NOT SENT|{message}");
                    }

                    await Task.Delay(random.Next(5000, 250000), _cancellationToken);
                }
            }
            catch (Exception e)
            {
                _log.Error($"Chat manage run error! {e}");
            }
        }
    }
}
