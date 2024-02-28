// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api;
using ghosts.api.Areas.Animator.Hubs;
using ghosts.api.Areas.Animator.Infrastructure.Animations.AnimationDefinitions;
using ghosts.api.Areas.Animator.Infrastructure.Models;
using Ghosts.Api.Infrastructure;
using Ghosts.Api.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using NLog;

namespace ghosts.api.Areas.Animator.Infrastructure.Animations;

public interface IManageableHostedService : IHostedService
{
    new Task StartAsync(CancellationToken cancellationToken);
    new Task StopAsync(CancellationToken cancellationToken);

    Task StartJob(AnimationConfiguration config, CancellationToken cancellationToken);
    Task StopJob(string jobId);
}

public class AnimationConfiguration
{
    public string JobId { get; set; }
    public string JobConfiguration { get; set; }
}

public class AnimationsManager : IManageableHostedService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ApplicationSettings _configuration;
    private readonly ApplicationDbContext _context;
    private readonly Random _random;
    private Thread _socialSharingJobThread;
    private Thread _socialGraphJobThread;
    private Thread _socialBeliefsJobThread;
    private Thread _chatJobThread;
    private Thread _fullAutonomyJobThread;
    
    private CancellationTokenSource _socialSharingJobCancellationTokenSource = new CancellationTokenSource();
    private CancellationTokenSource _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
    private CancellationTokenSource _socialBeliefsJobCancellationTokenSource = new CancellationTokenSource();
    private CancellationTokenSource _chatJobJobCancellationTokenSource = new CancellationTokenSource();
    private CancellationTokenSource _fullAutonomyCancellationTokenSource = new CancellationTokenSource();
    
    private readonly IHubContext<ActivityHub> _activityHubContext;

    public AnimationsManager(IHubContext<ActivityHub> activityHubContext, IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        // ReSharper disable once ConvertToPrimaryConstructor
        using var scope = _scopeFactory.CreateScope();
        this._context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        this._random = Random.Shared;
        this._activityHubContext = activityHubContext;
        this._configuration = Program.ApplicationSettings;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Info("Animations Manager initializing...");
        Run();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Info("Stopping Animations...");
        try
        {
            this._socialGraphJobCancellationTokenSource.Cancel();
            this._socialGraphJobThread?.Join();
        }
        catch
        {
            // ignore
        }

        try
        {
            this._socialSharingJobCancellationTokenSource.Cancel();
            this._socialSharingJobThread?.Join();
        }
        catch
        {
            // ignore
        }

        try
        {
            this._socialSharingJobCancellationTokenSource.Cancel();
            this._socialBeliefsJobThread?.Join();
        }
        catch
        {
            // ignore
        }
        
        try
        {
            this._chatJobJobCancellationTokenSource.Cancel();
            this._chatJobThread?.Join();
        }
        catch
        {
            // ignore
        }
        
        try
        {
            this._fullAutonomyCancellationTokenSource.Cancel();
            this._fullAutonomyJobThread?.Join();
        }
        catch
        {
            // ignore
        }

        _log.Info("Animations stopped.");
        
        return Task.CompletedTask;
    }

    public Task StartJob(AnimationConfiguration config, CancellationToken cancellationToken)
    {
        var jobId = Guid.NewGuid().ToString();
        _log.Info("Animations Manager initializing...");
        Run(config);
        return Task.CompletedTask;
    }

    public Task StopJob(string jobId)
    {
        _log.Info($"Stopping Animation {jobId}...");
        try
        {
            switch (jobId.ToUpper())
            {
                case "SOCIALGRAPH":
                    this._socialGraphJobCancellationTokenSource.Cancel();
                    this._socialGraphJobThread?.Join();
                    break;
                case "SOCIALSHARING":
                    this._socialSharingJobCancellationTokenSource.Cancel();
                    this._socialSharingJobThread?.Join();
                    break;
                case "SOCIALBELIEFS":
                    this._socialSharingJobCancellationTokenSource.Cancel();
                    this._socialBeliefsJobThread?.Join();
                    break;
                case "CHAT":
                    this._chatJobJobCancellationTokenSource.Cancel();
                    this._chatJobThread?.Join();
                    break;
                case "FULLAUTONOMY":
                    this._fullAutonomyCancellationTokenSource.Cancel();
                    this._fullAutonomyJobThread?.Join();
                    break;
            }
        }
        catch
        {
            // ignore
        }
        
        _log.Info($"Animation {jobId} stopped.");
        return Task.CompletedTask;
    }

    private void Run(AnimationConfiguration animationConfiguration)
    {
        _log.Info($"Attempting to start {animationConfiguration.JobId}...");
        var settings = _configuration;
        
        switch (animationConfiguration.JobId.ToUpper())
        {
            case "SOCIALGRAPH":
                var graphSettings = JsonConvert.DeserializeObject<ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.SocialGraphSettings>(animationConfiguration
                    .JobConfiguration);
                settings.AnimatorSettings.Animations.SocialGraph = graphSettings;
                if (graphSettings.IsMultiThreaded)
                {
                    _socialGraphJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialGraphJob(settings, this._context, this._random, this._activityHubContext, this._socialGraphJobCancellationTokenSource.Token);
                    });
                    _socialGraphJobThread.Start();
                }
                else
                {
                    _ = new SocialGraphJob(settings, this._context, this._random, this._activityHubContext, this._socialGraphJobCancellationTokenSource.Token);
                }
                
                break;
            case "SOCIALSHARING":
                var socialSettings = JsonConvert.DeserializeObject<ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.SocialSharingSettings>(animationConfiguration
                    .JobConfiguration);
                settings.AnimatorSettings.Animations.SocialSharing = socialSettings;
                if (socialSettings.IsMultiThreaded)
                {
                    _socialSharingJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialSharingJob(settings, this._context, this._random, this._activityHubContext, this._socialSharingJobCancellationTokenSource.Token);
                    });
                    _socialSharingJobThread.Start();
                }
                else
                {
                    _ = new SocialSharingJob(settings, this._context, this._random, this._activityHubContext, this._socialSharingJobCancellationTokenSource.Token);
                }
                
                break;
            case "SOCIALBELIEFS":
                var beliefSettings = JsonConvert.DeserializeObject<ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.SocialBeliefSettings>(animationConfiguration
                    .JobConfiguration);
                settings.AnimatorSettings.Animations.SocialBelief = beliefSettings;
                if (beliefSettings.IsMultiThreaded)
                {
                    _socialBeliefsJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialBeliefJob(settings, this._context, this._random, this._activityHubContext, this._socialBeliefsJobCancellationTokenSource.Token);
                    });
                    _socialBeliefsJobThread.Start();
                }
                else
                {
                    _ = new SocialBeliefJob(settings, this._context, this._random, this._activityHubContext, this._socialBeliefsJobCancellationTokenSource.Token);
                }
                
                break;
            case "CHAT":
                var chatSettings = JsonConvert.DeserializeObject<ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.ChatSettings>(animationConfiguration
                    .JobConfiguration);
                settings.AnimatorSettings.Animations.Chat = chatSettings;
                if (chatSettings.IsMultiThreaded)
                {
                    _chatJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new ChatJob(settings, this._context, this._random, this._activityHubContext, this._chatJobJobCancellationTokenSource.Token);
                    });
                    _chatJobThread.Start();
                }
                else
                {
                    _ = new ChatJob(settings, this._context, this._random, this._activityHubContext, this._chatJobJobCancellationTokenSource.Token);
                }
                
                break;
            case "FULLATONOMY":
                var autonomySettings = JsonConvert.DeserializeObject<ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.FullAutonomySettings>(animationConfiguration
                    .JobConfiguration);
                settings.AnimatorSettings.Animations.FullAutonomy = autonomySettings;
                if (autonomySettings.IsMultiThreaded)
                {
                    _fullAutonomyJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new FullAutonomyJob(settings, this._context, this._random, this._activityHubContext, this._fullAutonomyCancellationTokenSource.Token);
                    });
                    _fullAutonomyJobThread.Start();
                }
                else
                {
                    _ = new FullAutonomyJob(settings, this._context, this._random, this._activityHubContext, this._fullAutonomyCancellationTokenSource.Token);
                }
                
                break;
        }
    }

    private void Run()
    {
        if (!this._configuration.AnimatorSettings.Animations.IsEnabled)
        {
            _log.Info($"Animations are not enabled, exiting...");
            return;
        }

        _log.Info($"Animations are enabled, starting up...");

        try
        {
            if (this._configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled && this._configuration.AnimatorSettings.Animations.SocialGraph.IsInteracting)
            {
                _log.Info($"Starting SocialGraph...");
                if (this._configuration.AnimatorSettings.Animations.SocialGraph.IsMultiThreaded)
                {
                    _socialGraphJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialGraphJob(this._configuration, this._context, this._random, this._activityHubContext, this._socialGraphJobCancellationTokenSource.Token);
                    });
                    _socialGraphJobThread.Start();
                }
                else
                {
                    _ = new SocialGraphJob(this._configuration, this._context, this._random, this._activityHubContext, this._socialGraphJobCancellationTokenSource.Token);
                }
            }
            else
            {
                _log.Info($"SocialGraph is not enabled.");
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }

        try
        {
            if (this._configuration.AnimatorSettings.Animations.SocialBelief.IsEnabled && this._configuration.AnimatorSettings.Animations.SocialBelief.IsInteracting)
            {
                _log.Info($"Starting SocialBelief...");
                if (this._configuration.AnimatorSettings.Animations.SocialBelief.IsMultiThreaded)
                {
                    _socialBeliefsJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialBeliefJob(this._configuration, this._context, this._random, this._activityHubContext, this._socialBeliefsJobCancellationTokenSource.Token);
                    });
                    _socialBeliefsJobThread.Start();
                }
                else
                {
                    _ = new SocialBeliefJob(this._configuration, this._context, this._random, this._activityHubContext, this._socialBeliefsJobCancellationTokenSource.Token);
                }
            }
            else
            {
                _log.Info($"SocialBelief is not enabled.");
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        
        try
        {
            if (this._configuration.AnimatorSettings.Animations.Chat.IsEnabled && this._configuration.AnimatorSettings.Animations.Chat.IsInteracting)
            {
                _log.Info($"Starting chat job...");
                if (this._configuration.AnimatorSettings.Animations.Chat.IsMultiThreaded)
                {
                    _chatJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new ChatJob(this._configuration, this._context, this._random, this._activityHubContext, this._chatJobJobCancellationTokenSource.Token);
                    });
                    _chatJobThread.Start();
                }
                else
                {
                    _ = new ChatJob(this._configuration, this._context, this._random, this._activityHubContext, this._chatJobJobCancellationTokenSource.Token);
                }
            }
            else
            {
                _log.Info($"Chat job is not enabled.");
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }

        try
        {
            if (this._configuration.AnimatorSettings.Animations.SocialSharing.IsEnabled && this._configuration.AnimatorSettings.Animations.SocialSharing.IsInteracting)
            {
                _log.Info($"Starting SocialSharing...");
                if (this._configuration.AnimatorSettings.Animations.SocialSharing.IsMultiThreaded)
                {
                    _socialSharingJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialSharingJob(this._configuration, this._context, this._random, this._activityHubContext, this._socialSharingJobCancellationTokenSource.Token);
                    });
                    _socialSharingJobThread.Start();
                }
                else
                {
                    _ = new SocialSharingJob(this._configuration, this._context, this._random, this._activityHubContext, this._socialSharingJobCancellationTokenSource.Token);
                }
            }
            else
            {
                _log.Info($"SocialSharing is not enabled.");
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        
        try
        {
            if (this._configuration.AnimatorSettings.Animations.FullAutonomy.IsEnabled && this._configuration.AnimatorSettings.Animations.FullAutonomy.IsInteracting)
            {
                _log.Info($"Starting FullAutonomy...");
                if (this._configuration.AnimatorSettings.Animations.FullAutonomy.IsMultiThreaded)
                {
                    _fullAutonomyJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new FullAutonomyJob(this._configuration, this._context, this._random, this._activityHubContext, this._fullAutonomyCancellationTokenSource.Token);
                    });
                    _fullAutonomyJobThread.Start();
                }
                else
                {
                    _ = new FullAutonomyJob(this._configuration, this._context, this._random, this._activityHubContext, this._fullAutonomyCancellationTokenSource.Token);
                }
            }
            else
            {
                _log.Info($"FullAutonomy is not enabled.");
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
    }
}