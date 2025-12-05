// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using NCrontab;
using NLog;

namespace Ghosts.Api.Infrastructure.Animations.AnimationDefinitions;

public class WorkflowJobConfiguration
{
    public string WorkflowId { get; set; }
    public string WebhookUrl { get; set; }
    public string Schedule { get; set; } // Cron expression
}

public class WorkflowJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly WorkflowJobConfiguration _configuration;
    private readonly CancellationToken _cancellationToken;
    private readonly IHubContext<ActivityHub> _activityHubContext;

    public WorkflowJob(WorkflowJobConfiguration configuration, IHubContext<ActivityHub> activityHubContext, CancellationToken cancellationToken)
    {
        _configuration = configuration;
        _cancellationToken = cancellationToken;
        _activityHubContext = activityHubContext;

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        _log.Info($"Starting workflow job for workflow {_configuration.WorkflowId} with schedule {_configuration.Schedule}");

        CrontabSchedule schedule;
        try
        {
            schedule = CrontabSchedule.Parse(_configuration.Schedule);
        }
        catch (Exception ex)
        {
            _log.Error($"Invalid cron schedule '{_configuration.Schedule}': {ex.Message}");
            return;
        }

        var nextRun = schedule.GetNextOccurrence(DateTime.Now);

        while (!_cancellationToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var timeUntilNextRun = nextRun - now;

            if (timeUntilNextRun.TotalMilliseconds > 0)
            {
                // Wait until next scheduled run or cancellation
                try
                {
                    await Task.Delay(timeUntilNextRun, _cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    _log.Info($"Workflow job {_configuration.WorkflowId} cancelled during delay");
                    break;
                }
            }

            if (_cancellationToken.IsCancellationRequested)
                break;

            // Execute the workflow
            await ExecuteWorkflow();

            // Calculate next run time
            nextRun = schedule.GetNextOccurrence(DateTime.Now);
            _log.Info($"Next run for workflow {_configuration.WorkflowId} scheduled at {nextRun}");
        }

        _log.Info($"Workflow job {_configuration.WorkflowId} stopped");
    }

    private async Task ExecuteWorkflow()
    {
        try
        {
            _log.Info($"Executing workflow {_configuration.WorkflowId} at webhook {_configuration.WebhookUrl}");

            using var http = new HttpClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");

            // Call the webhook URL directly (typically GET for N8N webhooks)
            var response = await http.GetAsync(_configuration.WebhookUrl, _cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(_cancellationToken);
                _log.Info($"Workflow {_configuration.WorkflowId} executed successfully: {content}");

                // Notify via SignalR
                await _activityHubContext.Clients.All.SendAsync(
                    "workflow-executed",
                    new { workflowId = _configuration.WorkflowId, timestamp = DateTime.UtcNow, success = true },
                    _cancellationToken);
            }
            else
            {
                _log.Error($"Failed to execute workflow {_configuration.WorkflowId}: {response.StatusCode}");

                await _activityHubContext.Clients.All.SendAsync(
                    "workflow-executed",
                    new { workflowId = _configuration.WorkflowId, timestamp = DateTime.UtcNow, success = false },
                    _cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error executing workflow {_configuration.WorkflowId}");
        }
    }
}
