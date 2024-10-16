// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Ghosts.Animator.Extensions;
using ghosts.api.Hubs;
using Ghosts.Api.Infrastructure;
using ghosts.api.Infrastructure.ContentServices;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace ghosts.api.Infrastructure.Animations.AnimationDefinitions;

public class FullAutonomyJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly ApplicationSettings _configuration;
    private readonly ApplicationDbContext _context;
    private readonly Random _random;
    private const string SavePath = "_output/fullautonomy/";
    private readonly string _historyFile = $"{SavePath}/history.txt";
    private readonly List<string> _history;
    private readonly int _currentStep;
    private readonly IHubContext<ActivityHub> _activityHubContext;
    private readonly CancellationToken _cancellationToken;

    public FullAutonomyJob(ApplicationSettings configuration, IServiceScopeFactory scopeFactory, Random random,
        IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        try
        {
            this._activityHubContext = activityHubContext;
            this._configuration = configuration;
            this._random = random;
            
            using var innerScope = scopeFactory.CreateScope();
            this._context = innerScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            this._cancellationToken = cancellationToken;

            this._history = File.Exists(this._historyFile)
                ? [.. File.ReadAllLinesAsync(this._historyFile, cancellationToken).Result]
                : [];

            if (_configuration.AnimatorSettings.Animations.FullAutonomy.IsInteracting)
            {
                if (!Directory.Exists(SavePath))
                {
                    Directory.CreateDirectory(SavePath);
                }

                while (!this._cancellationToken.IsCancellationRequested)
                {
                    if (this._currentStep > _configuration.AnimatorSettings.Animations.FullAutonomy.MaximumSteps)
                    {
                        _log.Trace($"Maximum steps met: {this._currentStep - 1}. Full Autonomy is exiting...");
                        return;
                    }

                    this.Step();
                    Thread.Sleep(this._configuration.AnimatorSettings.Animations.FullAutonomy.TurnLength);

                    this._currentStep++;
                }
            }
        }
        catch (ThreadInterruptedException)
        {
            // continue
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    private async void Step()
    {
        var contentService = new ContentCreationService(_configuration.AnimatorSettings.Animations.FullAutonomy.ContentEngine);

        var agents = this._context.Npcs.ToList().Shuffle(_random).Take(_random.Next(5, 20));
        foreach (var agent in agents)
        {
            var history = this._history.Where(x => x.StartsWith(agent.Id.ToString()));
            var nextAction = await contentService.GenerateNextAction(agent, string.Join('\n', history));

            var line = $"{agent.Id}|{nextAction}|{DateTime.UtcNow}";
            line = $"{line.Replace(Environment.NewLine, "")}\n";

            await File.AppendAllTextAsync(_historyFile, line);
            this._history.Add(line);

            Thread.Sleep(500);

            // post to hub
            await this._activityHubContext.Clients.All.SendAsync("show",
                "1",
                agent.Id.ToString(),
                "social",
                nextAction,
                DateTime.Now.ToString(CultureInfo.InvariantCulture)
            );
        }

        await File.AppendAllTextAsync($"{SavePath}tweets.csv", this._history.ToString());
    }
}