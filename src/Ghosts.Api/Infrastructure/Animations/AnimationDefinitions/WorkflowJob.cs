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
    public string Schedule { get; set; }
    public string N8nApiUrl { get; set; }
    public string N8nApiKey { get; set; }
}

public class WorkflowJob
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly WorkflowJobConfiguration _configuration;
    private readonly CancellationToken _cancellationToken;
    private readonly IHubContext<ActivityHub> _activityHubContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public WorkflowJob(WorkflowJobConfiguration configuration, IHubContext<ActivityHub> activityHubContext, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        _configuration = configuration;
        _cancellationToken = cancellationToken;
        _activityHubContext = activityHubContext;
        _httpClientFactory = httpClientFactory;

        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        _log.Info($"Starting workflow job for workflow {_configuration.WorkflowId} with schedule {_configuration.Schedule}");

        CrontabSchedule schedule;
        try
        {
            var parts = _configuration.Schedule.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var options = new CrontabSchedule.ParseOptions { IncludingSeconds = parts.Length == 6 };
            schedule = CrontabSchedule.Parse(_configuration.Schedule, options);
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

            await ExecuteWorkflow();

            nextRun = schedule.GetNextOccurrence(DateTime.Now);
            _log.Info($"Next run for workflow {_configuration.WorkflowId} scheduled at {nextRun}");
        }

        _log.Info($"Workflow job {_configuration.WorkflowId} stopped");
    }

    /// <summary>
    /// Fetches the workflow's current state from n8n and returns the live webhook URL.
    /// Returns null if the workflow is inactive (caller should skip execution).
    /// Falls back to the stored WebhookUrl if n8n API credentials are unavailable.
    /// </summary>
    private async Task<string> ResolveCurrentWebhookUrlAsync()
    {
        if (string.IsNullOrEmpty(_configuration.N8nApiUrl) || string.IsNullOrEmpty(_configuration.N8nApiKey))
        {
            _log.Warn($"N8N_API_URL or N8N_API_KEY not configured — using stored webhook URL for workflow {_configuration.WorkflowId}");
            return _configuration.WebhookUrl;
        }

        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");
            http.DefaultRequestHeaders.Add("X-N8N-API-KEY", _configuration.N8nApiKey);

            var workflowApiUrl = $"{_configuration.N8nApiUrl.TrimEnd('/')}/{_configuration.WorkflowId}";
            var response = await http.GetAsync(workflowApiUrl, _cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _log.Warn($"Could not fetch workflow {_configuration.WorkflowId} from n8n ({(int)response.StatusCode}) — using stored webhook URL");
                return _configuration.WebhookUrl;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
            var workflow = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: _cancellationToken);

            // If the workflow has been deactivated, return null to signal skip
            if (workflow.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
            {
                _log.Warn($"Workflow {_configuration.WorkflowId} is no longer active in n8n");
                return null;
            }

            // Walk the nodes to find the current webhook path
            if (workflow.TryGetProperty("nodes", out var nodes))
            {
                foreach (var node in nodes.EnumerateArray())
                {
                    if (!node.TryGetProperty("type", out var typeProp)) continue;
                    if (typeProp.GetString() != "n8n-nodes-base.webhook") continue;
                    if (!node.TryGetProperty("parameters", out var parameters)) continue;
                    if (!parameters.TryGetProperty("path", out var pathProp)) continue;

                    var path = pathProp.GetString()?.Trim().TrimStart('/');
                    if (string.IsNullOrEmpty(path)) continue;

                    var apiUri = new Uri(_configuration.N8nApiUrl);
                    var port = apiUri.IsDefaultPort ? string.Empty : $":{apiUri.Port}";
                    var webhookUrl = $"{apiUri.Scheme}://{apiUri.Host}{port}/webhook/{path}";

                    if (webhookUrl != _configuration.WebhookUrl)
                        _log.Info($"Workflow {_configuration.WorkflowId} webhook URL updated: {_configuration.WebhookUrl} → {webhookUrl}");

                    return webhookUrl;
                }
            }

            // No webhook node found — fall back
            _log.Warn($"No webhook node found in workflow {_configuration.WorkflowId} — using stored webhook URL");
            return _configuration.WebhookUrl;
        }
        catch (Exception ex)
        {
            _log.Warn(ex, $"Failed to resolve current webhook URL for workflow {_configuration.WorkflowId} — using stored webhook URL");
            return _configuration.WebhookUrl;
        }
    }

    private async Task ExecuteWorkflow()
    {
        try
        {
            var webhookUrl = await ResolveCurrentWebhookUrlAsync();

            if (webhookUrl == null)
            {
                _log.Warn($"Skipping execution of workflow {_configuration.WorkflowId}: workflow is inactive in n8n");
                await _activityHubContext.Clients.All.SendAsync(
                    "workflow-executed",
                    new { workflowId = _configuration.WorkflowId, timestamp = DateTime.UtcNow, success = false, statusCode = 0, error = "Workflow is inactive in n8n" },
                    _cancellationToken);
                return;
            }

            _log.Info($"Executing workflow {_configuration.WorkflowId} at {webhookUrl}");

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");

            var response = await http.GetAsync(webhookUrl, _cancellationToken);
            var statusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(_cancellationToken);
                _log.Info($"Workflow {_configuration.WorkflowId} executed successfully ({statusCode}): {content}");

                await _activityHubContext.Clients.All.SendAsync(
                    "workflow-executed",
                    new { workflowId = _configuration.WorkflowId, timestamp = DateTime.UtcNow, success = true, statusCode },
                    _cancellationToken);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(_cancellationToken);
                var preview = body.Length > 200 ? body[..200] + "…" : body;
                _log.Error($"Failed to execute workflow {_configuration.WorkflowId} at {webhookUrl}: HTTP {statusCode} — {preview}");

                await _activityHubContext.Clients.All.SendAsync(
                    "workflow-executed",
                    new { workflowId = _configuration.WorkflowId, timestamp = DateTime.UtcNow, success = false, statusCode, error = preview },
                    _cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"Error executing workflow {_configuration.WorkflowId}");

            await _activityHubContext.Clients.All.SendAsync(
                "workflow-executed",
                new { workflowId = _configuration.WorkflowId, timestamp = DateTime.UtcNow, success = false, statusCode = 0, error = ex.Message },
                _cancellationToken);
        }
    }
}
