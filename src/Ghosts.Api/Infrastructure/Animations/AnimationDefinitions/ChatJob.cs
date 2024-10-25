// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ghosts.api.Hubs;
using ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat;
using ghosts.api.Infrastructure.ContentServices;
using Ghosts.Animator.Extensions;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions;

public class ChatJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationDbContext _context;
    private readonly Random _random;
    private readonly ChatClient _chatClient;
    private readonly int _currentStep;
    private readonly CancellationToken _cancellationToken;
    private readonly IFormatterService _formatterService;

    public ChatJob(ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.ChatSettings configuration, IServiceScopeFactory scopeFactory, Random random,
        IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        //todo: post results to activityHubContext for "top" reporting

        _random = random;

        using var innerScope = scopeFactory.CreateScope();
        _context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _cancellationToken = cancellationToken;

        var chatConfiguration = JsonSerializer.Deserialize<ChatJobConfiguration>(File.ReadAllText("config/chat.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException();

        _formatterService =
            new ContentCreationService(configuration.ContentEngine).FormatterService;

        _chatClient = new ChatClient(configuration, chatConfiguration, _formatterService, activityHubContext, _cancellationToken);

        while (!_cancellationToken.IsCancellationRequested)
        {
            if (_currentStep > configuration.MaximumSteps)
            {
                _log.Trace($"Maximum steps met: {_currentStep - 1}. Chat Job is exiting...");
                return;
            }

            Step(random, chatConfiguration);
            Thread.Sleep(configuration.TurnLength);

            _currentStep++;
        }
    }

    private async void Step(Random random, ChatJobConfiguration chatConfiguration)
    {
        _log.Trace("Executing a chat step...");
        var agents = _context.Npcs.ToList().Shuffle(_random).Take(chatConfiguration.Chat.AgentsPerBatch);
        await _chatClient.Step(random, agents);
    }
}
