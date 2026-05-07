// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
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

    // Track which scheduled workflow items have been handed off to AnimationsManager
    private readonly ConcurrentDictionary<int, bool> _scheduledItemsStarted = new();

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

        if (runningExecutions.Count == 0) return;

        var executionIds = runningExecutions.Select(e => e.Id).ToList();

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
                        _scheduledItemsStarted.TryAdd(item.Id, true);
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
            var webhookUrl = await ResolveWebhookUrlAsync(item.WorkflowId!, ct);
            if (webhookUrl == null)
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

            var response = await http.PostAsync(webhookUrl, content, ct);
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

                _log.Warn($"[Execution {item.ExecutionId}] Workflow item #{item.Number} failed ({statusCode}): {preview}");
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

        var webhookUrl = await ResolveWebhookUrlAsync(item.WorkflowId!, ct);
        if (webhookUrl == null)
        {
            _log.Warn($"[Execution {item.ExecutionId}] Cannot start scheduled workflow item #{item.Number}: webhook not resolved");
            return;
        }

        await _animationsManager.StartWorkflowJob(item.WorkflowId!, webhookUrl, item.Schedule!, ct);
    }

    private async Task<string> ResolveWebhookUrlAsync(string workflowId, CancellationToken ct)
    {
        var apiUrl = N8nConfig.GetApiUrl();
        var apiKey = N8nConfig.GetApiKey();

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
            return null;

        try
        {
            using var http = _httpClientFactory.CreateClient();
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("accept", "application/json");
            http.DefaultRequestHeaders.Add("X-N8N-API-KEY", apiKey);

            var workflowApiUrl = $"{apiUrl.TrimEnd('/')}/{workflowId}";
            var response = await http.GetAsync(workflowApiUrl, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var workflow = await JsonSerializer.DeserializeAsync<JsonElement>(stream, cancellationToken: ct);

            if (workflow.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
                return null;

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

                    var apiUri = new Uri(apiUrl);
                    var port = apiUri.IsDefaultPort ? string.Empty : $":{apiUri.Port}";
                    return $"{apiUri.Scheme}://{apiUri.Host}{port}/webhook/{path}";
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _log.Warn(ex, $"Failed to resolve webhook URL for workflow {workflowId}");
            return null;
        }
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
            _scheduledItemsStarted.TryRemove(itemId, out _);
        }

        await base.StopAsync(cancellationToken);
    }
}
