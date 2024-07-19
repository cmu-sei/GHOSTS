// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Ghosts.Animator.Extensions;
using ghosts.api.Hubs;
using Ghosts.Api.Infrastructure;
using ghosts.api.Infrastructure.Animations.AnimationDefinitions.Chat;
using ghosts.api.Infrastructure.ContentServices;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions;

public class ChatJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.ChatSettings _configuration;
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
        
        this._configuration = configuration;
        this._random = random;
        
        using var innerScope = scopeFactory.CreateScope();
        this._context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        this._cancellationToken = cancellationToken;

        var chatConfiguration = JsonSerializer.Deserialize<ChatJobConfiguration>(File.ReadAllText("config/chat.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException();

        this._formatterService =
            new ContentCreationService(_configuration.ContentEngine).FormatterService;
        
        this._chatClient = new ChatClient(_configuration, chatConfiguration, this._formatterService, activityHubContext, this._cancellationToken);
        
        while (!_cancellationToken.IsCancellationRequested)
        {
            if (this._currentStep > _configuration.MaximumSteps)
            {
                _log.Trace($"Maximum steps met: {this._currentStep - 1}. Chat Job is exiting...");
                return;
            }

            this.Step(random, chatConfiguration);
            Thread.Sleep(this._configuration.TurnLength);

            this._currentStep++;
        }
    }

    private async void Step(Random random, ChatJobConfiguration chatConfiguration)
    {
        _log.Trace("Executing a chat step...");
        var agents = this._context.Npcs.ToList().Shuffle(_random).Take(chatConfiguration.Chat.AgentsPerBatch);
        await this._chatClient.Step(random, agents);
    }
}