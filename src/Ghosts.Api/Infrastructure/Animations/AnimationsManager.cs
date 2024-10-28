// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ghosts.api.Hubs;
using ghosts.api.Infrastructure.Animations.AnimationDefinitions;
using ghosts.api.Infrastructure.Extensions;
using Ghosts.Api;
using Ghosts.Api.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;

namespace ghosts.api.Infrastructure.Animations;

public interface IManageableHostedService : IHostedService
{
    new Task StartAsync(CancellationToken cancellationToken);
    new Task StopAsync(CancellationToken cancellationToken);

    Task StartJob(AnimationConfiguration config, CancellationToken cancellationToken);
    Task StopJob(string jobId);

    IEnumerable<JobInfo> GetRunningJobs();

    string GetOutput(AnimationJobTypes job);
}

public class AnimationConfiguration
{
    public string JobId { get; set; }
    public string JobConfiguration { get; set; }
}

public class JobInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum AnimationJobTypes
{
    SOCIALGRAPH,
    SOCIALSHARING,
    SOCIALBELIEF,
    CHAT,
    FULLAUTONOMY
}

public class AnimationsManager(IHubContext<ActivityHub> activityHubContext, IServiceScopeFactory scopeFactory) : IManageableHostedService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private readonly ApplicationSettings _configuration = Program.ApplicationSettings;
    private readonly Random _random = Random.Shared;
    private Thread _socialSharingJobThread;
    private Thread _socialGraphJobThread;
    private Thread _socialBeliefsJobThread;
    private Thread _chatJobThread;
    private Thread _fullAutonomyJobThread;

    private CancellationTokenSource _socialSharingJobCancellationTokenSource = new();
    private CancellationTokenSource _socialGraphJobCancellationTokenSource = new();
    private readonly CancellationTokenSource _socialBeliefsJobCancellationTokenSource = new();
    private CancellationTokenSource _chatJobJobCancellationTokenSource = new();
    private readonly CancellationTokenSource _fullAutonomyCancellationTokenSource = new();

    private readonly IHubContext<ActivityHub> _activityHubContext = activityHubContext;
    private readonly ConcurrentDictionary<string, JobInfo> _jobs = new();

    public string GetOutput(AnimationJobTypes job)
    {
        var path = $"_output/{job.ToString().ToLower()}/";
        var outputZipFilePath = $"_output/{job.ToString().ToLower()}.zip";
        path.ZipDirectoryContents(outputZipFilePath);
        return outputZipFilePath;
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
            _socialGraphJobCancellationTokenSource.Cancel();
            _socialGraphJobThread?.Join();
            RemoveJob("SOCIALGRAPH");
            _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
        }
        catch
        {
            // ignore
        }

        try
        {
            _socialSharingJobCancellationTokenSource.Cancel();
            _socialSharingJobThread?.Join();
            RemoveJob("SOCIALSHARING");
            _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
        }
        catch
        {
            // ignore
        }

        try
        {
            _socialSharingJobCancellationTokenSource.Cancel();
            _socialBeliefsJobThread?.Join();
            RemoveJob("SOCIALBELIEF");
            _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
        }
        catch
        {
            // ignore
        }

        try
        {
            _chatJobJobCancellationTokenSource.Cancel();
            _chatJobThread?.Join();
            RemoveJob("CHAT");
            _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
        }
        catch
        {
            // ignore
        }

        try
        {
            _fullAutonomyCancellationTokenSource.Cancel();
            _fullAutonomyJobThread?.Join();
            RemoveJob("FULLAUTONOMY");
            _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
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
                    _socialGraphJobCancellationTokenSource.Cancel();
                    _socialGraphJobThread?.Join();
                    _socialGraphJobCancellationTokenSource = new CancellationTokenSource();
                    break;
                case "SOCIALSHARING":
                    _socialSharingJobCancellationTokenSource.Cancel();
                    _socialSharingJobThread?.Join();
                    _socialSharingJobCancellationTokenSource = new CancellationTokenSource();
                    break;
                case "SOCIALBELIEFS":
                    _socialSharingJobCancellationTokenSource.Cancel();
                    _socialBeliefsJobThread?.Join();
                    _socialSharingJobCancellationTokenSource = new CancellationTokenSource();
                    break;
                case "CHAT":
                    _chatJobJobCancellationTokenSource.Cancel();
                    _chatJobThread?.Join();
                    _chatJobJobCancellationTokenSource = new CancellationTokenSource();
                    break;
                case "FULLAUTONOMY":
                    _fullAutonomyCancellationTokenSource.Cancel();
                    _fullAutonomyJobThread?.Join();
                    _chatJobJobCancellationTokenSource = new CancellationTokenSource();
                    break;
            }

            RemoveJob(jobId.ToUpper());
        }
        catch
        {
            // ignore
        }

        _log.Info($"Animation {jobId} stopped.");
        return Task.CompletedTask;
    }

    public IEnumerable<JobInfo> GetRunningJobs()
    {
        return _jobs.Values.ToArray();
    }

    private void AddJob(string jobName)
    {
        var jobInfo = new JobInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = jobName,
            StartTime = DateTime.UtcNow,
        };
        _jobs.TryAdd(jobInfo.Name, jobInfo);
    }

    private bool RemoveJob(string jobName)
    {
        return _jobs.TryRemove(jobName, out _);
    }

    private void Run(AnimationConfiguration animationConfiguration)
    {
        if (string.IsNullOrEmpty(animationConfiguration.JobId)) return;

        _log.Info($"Attempting to start {animationConfiguration.JobId}...");
        var settings = _configuration;

        AddJob(animationConfiguration.JobId.ToUpper());

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
                        _ = new SocialGraphJob(settings, _scopeFactory, _random, _activityHubContext, _socialGraphJobCancellationTokenSource.Token);
                    });
                    _socialGraphJobThread.Start();
                }
                else
                {
                    _ = new SocialGraphJob(settings, _scopeFactory, _random, _activityHubContext, _socialGraphJobCancellationTokenSource.Token);
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
                        _ = new SocialSharingJob(settings, _scopeFactory, _random, _activityHubContext, _socialSharingJobCancellationTokenSource.Token);
                    });
                    _socialSharingJobThread.Start();
                }
                else
                {
                    _ = new SocialSharingJob(settings, _scopeFactory, _random, _activityHubContext, _socialSharingJobCancellationTokenSource.Token);
                }

                break;
            case "SOCIALBELIEF":
                var beliefSettings = JsonConvert.DeserializeObject<ApplicationSettings.AnimatorSettingsDetail.AnimationsSettings.SocialBeliefSettings>(animationConfiguration
                    .JobConfiguration);
                settings.AnimatorSettings.Animations.SocialBelief = beliefSettings;
                if (beliefSettings.IsMultiThreaded)
                {
                    _socialBeliefsJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialBeliefJob(settings, _scopeFactory, _random, _activityHubContext, _socialBeliefsJobCancellationTokenSource.Token);
                    });
                    _socialBeliefsJobThread.Start();
                }
                else
                {
                    _ = new SocialBeliefJob(settings, _scopeFactory, _random, _activityHubContext, _socialBeliefsJobCancellationTokenSource.Token);
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
                        _ = new ChatJob(chatSettings, _scopeFactory, _random, _activityHubContext, _chatJobJobCancellationTokenSource.Token);
                    });
                    _chatJobThread.Start();
                }
                else
                {
                    _ = new ChatJob(chatSettings, _scopeFactory, _random, _activityHubContext, _chatJobJobCancellationTokenSource.Token);
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
                        _ = new FullAutonomyJob(settings, _scopeFactory, _random, _activityHubContext, _fullAutonomyCancellationTokenSource.Token);
                    });
                    _fullAutonomyJobThread.Start();
                }
                else
                {
                    _ = new FullAutonomyJob(settings, _scopeFactory, _random, _activityHubContext, _fullAutonomyCancellationTokenSource.Token);
                }

                break;
        }
    }

    private void Run()
    {
        if (!_configuration.AnimatorSettings.Animations.IsEnabled)
        {
            _log.Info($"Animations are not enabled, exiting...");
            return;
        }

        _log.Info($"Animations are enabled, starting up...");

        try
        {
            if (_configuration.AnimatorSettings.Animations.SocialGraph.IsEnabled && _configuration.AnimatorSettings.Animations.SocialGraph.IsInteracting)
            {
                _log.Info($"Starting SocialGraph...");
                if (_configuration.AnimatorSettings.Animations.SocialGraph.IsMultiThreaded)
                {
                    _socialGraphJobThread = new Thread(() =>
                    {
                        AddJob("SOCIALGRAPH");

                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialGraphJob(_configuration, _scopeFactory, _random, _activityHubContext, _socialGraphJobCancellationTokenSource.Token);
                    });
                    _socialGraphJobThread.Start();
                }
                else
                {
                    _ = new SocialGraphJob(_configuration, _scopeFactory, _random, _activityHubContext, _socialGraphJobCancellationTokenSource.Token);
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
            if (_configuration.AnimatorSettings.Animations.SocialBelief.IsEnabled && _configuration.AnimatorSettings.Animations.SocialBelief.IsInteracting)
            {
                _log.Info($"Starting SocialBelief...");
                if (_configuration.AnimatorSettings.Animations.SocialBelief.IsMultiThreaded)
                {
                    AddJob("SOCIALBELIEF");

                    _socialBeliefsJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialBeliefJob(_configuration, _scopeFactory, _random, _activityHubContext, _socialBeliefsJobCancellationTokenSource.Token);
                    });
                    _socialBeliefsJobThread.Start();
                }
                else
                {
                    _ = new SocialBeliefJob(_configuration, _scopeFactory, _random, _activityHubContext, _socialBeliefsJobCancellationTokenSource.Token);
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
            if (_configuration.AnimatorSettings.Animations.Chat.IsEnabled && _configuration.AnimatorSettings.Animations.Chat.IsInteracting)
            {
                _log.Info($"Starting chat job...");
                if (_configuration.AnimatorSettings.Animations.Chat.IsMultiThreaded)
                {
                    AddJob("CHAT");

                    _chatJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new ChatJob(_configuration.AnimatorSettings.Animations.Chat, _scopeFactory, _random, _activityHubContext, _chatJobJobCancellationTokenSource.Token);
                    });
                    _chatJobThread.Start();
                }
                else
                {
                    _ = new ChatJob(_configuration.AnimatorSettings.Animations.Chat, _scopeFactory, _random, _activityHubContext, _chatJobJobCancellationTokenSource.Token);
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
            if (_configuration.AnimatorSettings.Animations.SocialSharing.IsEnabled && _configuration.AnimatorSettings.Animations.SocialSharing.IsInteracting)
            {
                _log.Info($"Starting SocialSharing...");
                if (_configuration.AnimatorSettings.Animations.SocialSharing.IsMultiThreaded)
                {
                    AddJob("SOCIALSHARING");

                    _socialSharingJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new SocialSharingJob(_configuration, _scopeFactory, _random, _activityHubContext, _socialSharingJobCancellationTokenSource.Token);
                    });
                    _socialSharingJobThread.Start();
                }
                else
                {
                    _ = new SocialSharingJob(_configuration, _scopeFactory, _random, _activityHubContext, _socialSharingJobCancellationTokenSource.Token);
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
            if (_configuration.AnimatorSettings.Animations.FullAutonomy.IsEnabled && _configuration.AnimatorSettings.Animations.FullAutonomy.IsInteracting)
            {
                _log.Info($"Starting FullAutonomy...");
                if (_configuration.AnimatorSettings.Animations.FullAutonomy.IsMultiThreaded)
                {
                    AddJob("FULLAUTONOMY");

                    _fullAutonomyJobThread = new Thread(() =>
                    {
                        Thread.CurrentThread.IsBackground = true;
                        _ = new FullAutonomyJob(_configuration, _scopeFactory, _random, _activityHubContext, _fullAutonomyCancellationTokenSource.Token);
                    });
                    _fullAutonomyJobThread.Start();
                }
                else
                {
                    _ = new FullAutonomyJob(_configuration, _scopeFactory, _random, _activityHubContext, _fullAutonomyCancellationTokenSource.Token);
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
