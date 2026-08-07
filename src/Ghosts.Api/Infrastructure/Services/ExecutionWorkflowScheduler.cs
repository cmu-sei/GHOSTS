// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Hubs;
using Ghosts.Api.Infrastructure.Animations;
using Ghosts.Api.Infrastructure.Data;
using Ghosts.Api.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Ghosts.Api.Infrastructure.Services;

/// <summary>
/// Monitors running executions and fires workflow timeline items when their time offset expires.
/// For PointInTime items: triggers when elapsed time since execution start >= parsed "T+Xm" offset.
/// For Scheduled items: triggers on cron schedule (delegated to AnimationsManager).
/// </summary>
public class ExecutionWorkflowScheduler : BackgroundService
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<ExecutionHub> _executionHub;
    private readonly IManageableHostedService _animationsManager;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    // Track scheduled workflow items handed off to AnimationsManager: item id → job key,
    // so a job can be stopped when its execution ends.
    private readonly ConcurrentDictionary<int, string> _scheduledItemsStarted = new();

    public ExecutionWorkflowScheduler(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IHubContext<ExecutionHub> executionHub,
        IManageableHostedService animationsManager)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _executionHub = executionHub;
        _animationsManager = animationsManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.Info("ExecutionWorkflowScheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueWorkflowItemsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error in ExecutionWorkflowScheduler poll cycle");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }

        _log.Info("ExecutionWorkflowScheduler stopped");
    }

    private async Task ProcessDueWorkflowItemsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var runningExecutions = await context.Set<Execution>()
            .Where(e => e.Status == ExecutionStatus.Running && e.StartedAt != null)
            .ToListAsync(ct);

        var executionIds = runningExecutions.Select(e => e.Id).ToList();

        // Stop scheduled workflow jobs whose execution is no longer running so a
        // finished/paused/cancelled run doesn't keep poking n8n.
        await StopJobsForEndedExecutionsAsync(context, executionIds, ct);

        if (runningExecutions.Count == 0) return;

        var pendingWorkflowItems = await context.Set<ExecutionTimelineItem>()
            .Where(ti => executionIds.Contains(ti.ExecutionId)
                         && ti.AutomationKind == "Workflow"
                         && (ti.Status == "Pending" || ti.Status == "Queued")
                         && ti.WorkflowId != null)
            .ToListAsync(ct);

        if (pendingWorkflowItems.Count == 0) return;

        var now = DateTime.UtcNow;

        foreach (var item in pendingWorkflowItems)
        {
            var execution = runningExecutions.First(e => e.Id == item.ExecutionId);
            var elapsed = now - execution.StartedAt!.Value;

            if (item.TriggerKind == TriggerKind.PointInTime)
            {
                var offsetMs = ExecutionService.ParseTimeOffsetMs(item.Time);
                if (elapsed.TotalMilliseconds >= offsetMs)
                {
                    await FireWorkflowItemAsync(context, item, execution, ct);
                }
            }
            else if (item.TriggerKind == TriggerKind.Scheduled && !string.IsNullOrEmpty(item.Schedule))
            {
                if (!_scheduledItemsStarted.ContainsKey(item.Id))
                {
                    var offsetMs = ExecutionService.ParseTimeOffsetMs(item.Time);
                    if (elapsed.TotalMilliseconds >= offsetMs)
                    {
                        await StartScheduledWorkflowItemAsync(item, ct);
                        _scheduledItemsStarted.TryAdd(item.Id, WorkflowJobKey(item));
                    }
                }
            }
        }
    }

    private async Task FireWorkflowItemAsync(
        ApplicationDbContext context,
        ExecutionTimelineItem item,
        Execution execution,
        CancellationToken ct)
    {
        _log.Info($"[Execution {execution.Id}] Firing workflow item #{item.Number} (workflow {item.WorkflowId}) at elapsed {(DateTime.UtcNow - execution.StartedAt!.Value).TotalMinutes:F1}m");

        item.Status = "Deployed";
        item.LastFiredAt = DateTime.UtcNow;
        item.FireCount++;
        await context.SaveChangesAsync(ct);

        await BroadcastTimelineItemUpdateAsync(item.ExecutionId, item.Id, "Deployed");

        try
        {
            await EnsureWorkflowActiveAsync(item.WorkflowId!, ct);
            var webhookInfo = await ResolveWebhookInfoAsync(item.WorkflowId!, ct);
            if (webhookInfo == null)
            {
                item.Status = "Failed";
                item.CompletedAt = DateTime.UtcNow;
                item.CompletedBy = "workflow-scheduler";
                item.ResultData = JsonSerializer.Serialize(new { error = "Workflow inactive or webhook not found" });

                context.Set<ExecutionEvent>().Add(new ExecutionEvent
                {
                    ExecutionId = item.ExecutionId,
                    Timestamp = DateTime.UtcNow,
                    EventType = "WorkflowFailed",
                    Description = $"Workflow item #{item.Number} failed: workflow inactive or webhook not found",
                    Data = JsonSerializer.Serialize(new { itemId = item.Id, workflowId = item.WorkflowId }),
                    Severity = "Warning"
                });

                await context.SaveChangesAsync(ct);
                await BroadcastTimelineItemUpdateAsync(item.ExecutionId, item.Id, "Failed");
                await CheckAndAutoCompleteAsync(context, item.ExecutionId, ct);
                return;
            }

            var webhookUrl = webhookInfo.Url;

            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");

            var payload = new
            {
                executionId = item.ExecutionId,
                timelineItemId = item.Id,
                timelineItemNumber = item.Number,
                description = item.Description,
                assigned = item.Assigned
            };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            _log.Info($"[Execution {execution.Id}] Sending {webhookInfo.HttpMethod} to {webhookUrl}");

            HttpResponseMessage response;
            if (webhookInfo.HttpMethod == "POST")
            {
                response = await http.PostAsync(webhookUrl, content, ct);
            }
            else
            {
                response = await http.GetAsync(webhookUrl, ct);
            }

            var statusCode = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                item.Status = "Completed";
                item.CompletedAt = DateTime.UtcNow;
                item.CompletedBy = "workflow-scheduler";
                item.ResultData = body.Length > 4000 ? body[..4000] : body;

                context.Set<ExecutionEvent>().Add(new ExecutionEvent
                {
                    ExecutionId = item.ExecutionId,
                    Timestamp = DateTime.UtcNow,
                    EventType = "WorkflowCompleted",
                    Description = $"Workflow item #{item.Number} completed (HTTP {statusCode})",
                    Data = JsonSerializer.Serialize(new { itemId = item.Id, workflowId = item.WorkflowId, statusCode }),
                    Severity = "Info"
                });

                _log.Info($"[Execution {item.ExecutionId}] Workflow item #{item.Number} completed ({statusCode})");
            }
            else
            {
                item.Status = "Failed";
                item.CompletedAt = DateTime.UtcNow;
                item.CompletedBy = "workflow-scheduler";
                var preview = body.Length > 200 ? body[..200] : body;
                item.ResultData = JsonSerializer.Serialize(new { statusCode, error = preview });

                context.Set<ExecutionEvent>().Add(new ExecutionEvent
                {
                    ExecutionId = item.ExecutionId,
                    Timestamp = DateTime.UtcNow,
                    EventType = "WorkflowFailed",
                    Description = $"Workflow item #{item.Number} failed (HTTP {statusCode})",
                    Data = JsonSerializer.Serialize(new { itemId = item.Id, workflowId = item.WorkflowId, statusCode, error = preview }),
                    Severity = "Warning"
                });

                _log.Warn($"[Execution {item.ExecutionId}] Workflow item #{item.Number} failed: {webhookInfo.HttpMethod} {webhookUrl} returned {statusCode}: {preview}");
            }

            await context.SaveChangesAsync(ct);
            await BroadcastTimelineItemUpdateAsync(item.ExecutionId, item.Id, item.Status);
            await CheckAndAutoCompleteAsync(context, item.ExecutionId, ct);
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"[Execution {item.ExecutionId}] Error firing workflow item #{item.Number}");

            item.Status = "Failed";
            item.CompletedAt = DateTime.UtcNow;
            item.CompletedBy = "workflow-scheduler";
            item.ResultData = JsonSerializer.Serialize(new { error = ex.Message });

            context.Set<ExecutionEvent>().Add(new ExecutionEvent
            {
                ExecutionId = item.ExecutionId,
                Timestamp = DateTime.UtcNow,
                EventType = "WorkflowFailed",
                Description = $"Workflow item #{item.Number} failed: {ex.Message}",
                Data = JsonSerializer.Serialize(new { itemId = item.Id, workflowId = item.WorkflowId, error = ex.Message }),
                Severity = "Error"
            });

            await context.SaveChangesAsync(ct);
            await BroadcastTimelineItemUpdateAsync(item.ExecutionId, item.Id, "Failed");
            await CheckAndAutoCompleteAsync(context, item.ExecutionId, ct);
        }
    }

    private async Task StartScheduledWorkflowItemAsync(ExecutionTimelineItem item, CancellationToken ct)
    {
        _log.Info($"[Execution {item.ExecutionId}] Starting scheduled workflow item #{item.Number} (workflow {item.WorkflowId}, schedule: {item.Schedule})");

        await EnsureWorkflowActiveAsync(item.WorkflowId!, ct);
        var webhookInfo = await ResolveWebhookInfoAsync(item.WorkflowId!, ct);
        if (webhookInfo == null)
        {
            _log.Warn($"[Execution {item.ExecutionId}] Cannot start scheduled workflow item #{item.Number}: webhook not resolved");
            return;
        }

        _log.Info($"[Execution {item.ExecutionId}] Scheduled workflow item #{item.Number} will use {webhookInfo.HttpMethod} {webhookInfo.Url}");
        await _animationsManager.StartWorkflowJob(item.WorkflowId!, webhookInfo.Url, item.Schedule!, ct, WorkflowJobKey(item));
    }

    /// <summary>Per-execution job key so concurrent runs of the same workflow don't collide.</summary>
    private static string WorkflowJobKey(ExecutionTimelineItem item) => $"exec-{item.ExecutionId}-item-{item.Id}";

    /// <summary>
    /// Stops scheduled workflow jobs for items whose execution is no longer running, and
    /// forgets them so they can be re-scheduled if the execution resumes.
    /// </summary>
    private async Task StopJobsForEndedExecutionsAsync(ApplicationDbContext context, List<int> runningExecutionIds, CancellationToken ct)
    {
        if (_scheduledItemsStarted.IsEmpty) return;

        var startedItemIds = _scheduledItemsStarted.Keys.ToList();
        var itemExecutions = await context.Set<ExecutionTimelineItem>()
            .Where(ti => startedItemIds.Contains(ti.Id))
            .Select(ti => new { ti.Id, ti.ExecutionId })
            .ToListAsync(ct);

        foreach (var itemId in startedItemIds)
        {
            var mapping = itemExecutions.FirstOrDefault(x => x.Id == itemId);
            // Stop if the item is gone (deleted) or its execution is no longer running.
            var stillRunning = mapping != null && runningExecutionIds.Contains(mapping.ExecutionId);
            if (stillRunning) continue;

            if (_scheduledItemsStarted.TryRemove(itemId, out var jobKey))
            {
                await _animationsManager.StopWorkflowJob(jobKey);
                _log.Info($"Stopped scheduled workflow job {jobKey} (execution ended)");
            }
        }
    }

    private record WebhookInfo(string Url, string HttpMethod);

    /// <summary>
    /// Ensures the referenced workflow is active in n8n, activating it via the API if not
    /// (the provisioned key carries the workflow:activate scope). Default workflows are
    /// imported inactive, so without this their webhooks 404 and the item fails. Accepts an
    /// n8n id or a stable webhook ref/name. Best-effort: logs and returns on any failure.
    /// </summary>
    private async Task EnsureWorkflowActiveAsync(string workflowIdOrRef, CancellationToken ct)
    {
        var apiUrl = N8nConfig.GetApiUrl();
        var apiKey = N8nConfig.GetApiKey();
        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey)) return;

        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");
            http.DefaultRequestHeaders.Add("X-N8N-API-KEY", apiKey);

            // Resolve the live workflow (id, active state) from an id or a ref.
            string id = null;
            var isActive = false;

            var directUrl = $"{apiUrl.TrimEnd('/')}/{workflowIdOrRef}";
            var direct = await http.GetAsync(directUrl, ct);
            if (direct.IsSuccessStatusCode)
            {
                await using var s = await direct.Content.ReadAsStreamAsync(ct);
                var wf = await JsonSerializer.DeserializeAsync<JsonElement>(s, cancellationToken: ct);
                id = wf.TryGetProperty("id", out var idp) ? idp.GetString() : workflowIdOrRef;
                isActive = wf.TryGetProperty("active", out var ap) && ap.GetBoolean();
            }
            else
            {
                var listResp = await http.GetAsync(apiUrl, ct);
                if (!listResp.IsSuccessStatusCode) return;
                await using var ls = await listResp.Content.ReadAsStreamAsync(ct);
                var list = await JsonSerializer.DeserializeAsync<JsonElement>(ls, cancellationToken: ct);
                if (!list.TryGetProperty("data", out var data)) return;
                foreach (var wf in data.EnumerateArray())
                {
                    if (!WorkflowMatchesRef(wf, workflowIdOrRef)) continue;
                    id = wf.TryGetProperty("id", out var idp) ? idp.GetString() : null;
                    isActive = wf.TryGetProperty("active", out var ap) && ap.GetBoolean();
                    break;
                }
            }

            if (id == null || isActive) return;

            var activateResp = await http.PostAsync($"{apiUrl.TrimEnd('/')}/{id}/activate", null, ct);
            if (activateResp.IsSuccessStatusCode)
                _log.Info($"[Workflow {workflowIdOrRef}] Activated in n8n (id {id})");
            else
                _log.Warn($"[Workflow {workflowIdOrRef}] Activate failed ({(int)activateResp.StatusCode})");
        }
        catch (Exception ex)
        {
            _log.Warn(ex, $"[Workflow {workflowIdOrRef}] Failed to ensure workflow active");
        }
    }

    /// <summary>
    /// Resolves an item's WorkflowId to a live webhook. The value is either a real n8n
    /// workflow id (hand-authored timeline events) or a stable webhook ref such as "beliefs"
    /// (default scenario bindings, since REST import can reassign the n8n id). Tries a direct
    /// id fetch first, then falls back to listing workflows and matching by webhook path or name.
    /// </summary>
    private async Task<WebhookInfo> ResolveWebhookInfoAsync(string workflowIdOrRef, CancellationToken ct)
    {
        var apiUrl = N8nConfig.GetApiUrl();
        var apiKey = N8nConfig.GetApiKey();

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
        {
            _log.Warn($"[Workflow {workflowIdOrRef}] N8N_API_URL or N8N_API_KEY not configured");
            return null;
        }

        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");
            http.DefaultRequestHeaders.Add("X-N8N-API-KEY", apiKey);

            // 1. Try treating the value as a real n8n workflow id.
            var directUrl = $"{apiUrl.TrimEnd('/')}/{workflowIdOrRef}";
            var response = await http.GetAsync(directUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                var workflow = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);
                return ExtractWebhookInfo(workflow, apiUrl, workflowIdOrRef);
            }

            // 2. Fall back: list workflows and match by webhook path or name.
            _log.Debug($"[Workflow {workflowIdOrRef}] Direct fetch failed ({(int)response.StatusCode}); matching by webhook ref");
            var listResp = await http.GetAsync(apiUrl, ct);
            if (!listResp.IsSuccessStatusCode)
            {
                _log.Warn($"[Workflow {workflowIdOrRef}] Failed to list workflows from n8n API ({(int)listResp.StatusCode})");
                return null;
            }

            await using var listStream = await listResp.Content.ReadAsStreamAsync(ct);
            var list = await JsonSerializer.DeserializeAsync<JsonElement>(listStream, cancellationToken: ct);
            if (!list.TryGetProperty("data", out var data)) return null;

            foreach (var wf in data.EnumerateArray())
            {
                if (WorkflowMatchesRef(wf, workflowIdOrRef))
                {
                    var info = ExtractWebhookInfo(wf, apiUrl, workflowIdOrRef);
                    if (info != null) return info;
                }
            }

            _log.Warn($"[Workflow {workflowIdOrRef}] No active workflow found matching ref");
            return null;
        }
        catch (Exception ex)
        {
            _log.Warn(ex, $"[Workflow {workflowIdOrRef}] Failed to resolve webhook URL");
            return null;
        }
    }

    /// <summary>True if the workflow's name or a webhook node's path equals the ref.</summary>
    private static bool WorkflowMatchesRef(JsonElement workflow, string workflowRef)
    {
        if (workflow.TryGetProperty("name", out var nameProp)
            && string.Equals(nameProp.GetString(), workflowRef, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!workflow.TryGetProperty("nodes", out var nodes)) return false;
        foreach (var node in nodes.EnumerateArray())
        {
            if (!node.TryGetProperty("type", out var typeProp) || typeProp.GetString() != "n8n-nodes-base.webhook") continue;
            if (!node.TryGetProperty("parameters", out var parameters) || !parameters.TryGetProperty("path", out var pathProp)) continue;
            var path = pathProp.GetString()?.Trim().TrimStart('/');
            if (string.Equals(path, workflowRef.Trim().TrimStart('/'), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Extracts a webhook URL + method from a workflow definition, or null if inactive / no webhook.</summary>
    private static WebhookInfo ExtractWebhookInfo(JsonElement workflow, string apiUrl, string label)
    {
        if (workflow.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
        {
            _log.Warn($"[Workflow {label}] Workflow is inactive in n8n");
            return null;
        }

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

                var httpMethod = "GET";
                if (parameters.TryGetProperty("httpMethod", out var methodProp))
                {
                    httpMethod = methodProp.GetString()?.ToUpperInvariant() ?? "GET";
                }

                var apiUri = new Uri(apiUrl);
                var port = apiUri.IsDefaultPort ? string.Empty : $":{apiUri.Port}";
                var webhookUrl = $"{apiUri.Scheme}://{apiUri.Host}{port}/webhook/{path}";

                _log.Info($"[Workflow {label}] Resolved webhook: {httpMethod} {webhookUrl}");
                return new WebhookInfo(webhookUrl, httpMethod);
            }
        }

        _log.Warn($"[Workflow {label}] No webhook node found in workflow definition");
        return null;
    }

    private async Task BroadcastTimelineItemUpdateAsync(int executionId, int itemId, string status)
    {
        try
        {
            var connections = ExecutionHub.GetConnections();
            foreach (var connectionId in connections.GetConnections(executionId.ToString()))
            {
                await _executionHub.Clients.Client(connectionId)
                    .SendAsync("ExecutionUpdate", new { executionId, updateType = "TimelineItemUpdate", data = new { itemId, status }, timestamp = DateTime.UtcNow });
            }
            foreach (var connectionId in connections.GetConnections("all"))
            {
                await _executionHub.Clients.Client(connectionId)
                    .SendAsync("ExecutionUpdate", new { executionId, updateType = "TimelineItemUpdate", data = new { itemId, status }, timestamp = DateTime.UtcNow });
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Failed to broadcast timeline item update: {ex.Message}");
        }
    }

    private static async Task CheckAndAutoCompleteAsync(ApplicationDbContext context, int executionId, CancellationToken ct)
    {
        var execution = await context.Set<Execution>().FindAsync(executionId);
        if (execution == null || execution.Status != ExecutionStatus.Running)
            return;

        var items = await context.Set<ExecutionTimelineItem>()
            .Where(ti => ti.ExecutionId == executionId)
            .ToListAsync(ct);

        if (items.Count == 0) return;

        var terminalStatuses = new[] { "Completed", "Failed", "Skipped" };
        if (!items.All(ti => terminalStatuses.Contains(ti.Status))) return;

        execution.Status = ExecutionStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;

        context.Set<ExecutionEvent>().Add(new ExecutionEvent
        {
            ExecutionId = executionId,
            Timestamp = DateTime.UtcNow,
            EventType = "StatusChange",
            Description = "Execution auto-completed: all timeline items are in terminal state",
            Data = JsonSerializer.Serialize(new { status = "Completed", timestamp = DateTime.UtcNow }),
            Severity = "Info"
        });

        await context.SaveChangesAsync(ct);
        _log.Info($"Execution {executionId} auto-completed via workflow scheduler");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop any scheduled workflow jobs that were started for execution items
        foreach (var itemId in _scheduledItemsStarted.Keys)
        {
            if (_scheduledItemsStarted.TryRemove(itemId, out var jobKey))
                await _animationsManager.StopWorkflowJob(jobKey);
        }

        await base.StopAsync(cancellationToken);
    }
}
