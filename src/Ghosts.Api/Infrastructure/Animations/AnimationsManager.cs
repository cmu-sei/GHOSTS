// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api;
using Ghosts.Api.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Ghosts.Api.Infrastructure.Animations;

public interface IManageableHostedService : IHostedService
{
    new Task StartAsync(CancellationToken cancellationToken);
    new Task StopAsync(CancellationToken cancellationToken);

    Task StartWorkflowJob(string workflowId, string webhookUrl, string schedule, CancellationToken cancellationToken, string jobKey = null);
    Task StopWorkflowJob(string jobKey);
    bool IsWorkflowRunning(string jobKey);

    IEnumerable<JobInfo> GetRunningJobs();
}

public class JobInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime StartTime { get; set; }
}

public class AnimationsManager(IHubContext<ActivityHub> activityHubContext, IHttpClientFactory httpClientFactory) : IManageableHostedService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    private readonly IHubContext<ActivityHub> _activityHubContext = activityHubContext;
    private readonly ConcurrentDictionary<string, JobInfo> _jobs = new();

    // Workflow job tracking
    private readonly ConcurrentDictionary<string, (Thread thread, CancellationTokenSource cts)> _workflowJobs = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _log.Info("Animations Manager initializing...");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _log.Info("Stopping Animations...");

        // Stop all workflow jobs
        foreach (var workflowId in _workflowJobs.Keys.ToArray())
        {
            try
            {
                _ = StopWorkflowJob(workflowId);
            }
            catch
            {
                // ignore
            }
        }

        _log.Info("Animations stopped.");

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

    public Task StartWorkflowJob(string workflowId, string webhookUrl, string schedule, CancellationToken cancellationToken, string jobKey = null)
    {
        // jobKey distinguishes concurrent runs of the same workflow (e.g. per-execution);
        // defaults to workflowId to preserve the standalone workflow-runner behavior.
        jobKey ??= workflowId;
        _log.Info($"Starting workflow job {jobKey} for workflow {workflowId} with schedule {schedule}");

        // Check if already running
        if (_workflowJobs.ContainsKey(jobKey))
        {
            _log.Warn($"Workflow job {jobKey} is already running");
            return Task.CompletedTask;
        }

        var cts = new CancellationTokenSource();
        var config = new AnimationDefinitions.WorkflowJobConfiguration
        {
            WorkflowId = workflowId,
            WebhookUrl = webhookUrl,
            Schedule = schedule,
            N8nApiUrl = N8nConfig.GetApiUrl(),
            N8nApiKey = N8nConfig.GetApiKey()
        };

        var thread = new Thread(() =>
        {
            Thread.CurrentThread.IsBackground = true;
            _ = new AnimationDefinitions.WorkflowJob(config, _activityHubContext, _httpClientFactory, cts.Token);
        });

        _workflowJobs.TryAdd(jobKey, (thread, cts));
        AddJob($"WORKFLOW_{jobKey}");
        thread.Start();

        return Task.CompletedTask;
    }

    public Task StopWorkflowJob(string jobKey)
    {
        _log.Info($"Stopping workflow job {jobKey}");

        if (_workflowJobs.TryRemove(jobKey, out var jobInfo))
        {
            try
            {
                jobInfo.cts.Cancel();
                jobInfo.thread?.Join(TimeSpan.FromSeconds(5)); // Wait up to 5 seconds for graceful shutdown
                jobInfo.cts.Dispose();
                RemoveJob($"WORKFLOW_{jobKey}");
                _log.Info($"Workflow job {jobKey} stopped successfully");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Error stopping workflow job {jobKey}");
            }
        }
        else
        {
            _log.Warn($"Workflow job {jobKey} not found");
        }

        return Task.CompletedTask;
    }

    public bool IsWorkflowRunning(string jobKey)
    {
        return _workflowJobs.ContainsKey(jobKey);
    }
}
