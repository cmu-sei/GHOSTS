// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Animations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[Route("api/animations")]
[Produces("application/json")]
public class AnimationJobsController(IServiceProvider serviceProvider) : Controller
{
    private static Logger _log = LogManager.GetCurrentClassLogger();
    private readonly AnimationsManager _animationsManager = serviceProvider.GetRequiredService<IManageableHostedService>() as AnimationsManager;

    [SwaggerOperation("AnimationJobsGet")]
    [HttpGet("jobs")]
    public IEnumerable<JobInfo> Get(CancellationToken cancellationToken)
    {
        return _animationsManager.GetRunningJobs();
    }

    [HttpPost("start")]
    public IActionResult Start(AnimationConfiguration configuration, [FromForm] string jobConfiguration)
    {
        configuration.JobConfiguration = jobConfiguration;
        _animationsManager.StartJob(configuration, CancellationToken.None);
        return Ok();
    }

    [HttpPost("stop")]
    public IActionResult Stop(string jobId)
    {
        _animationsManager.StopJob(jobId);
        return Ok();
    }

    [SwaggerOperation("WorkflowsGet")]
    [HttpGet("workflows")]
    public async Task<IActionResult> Workflows(CancellationToken cancellationToken)
    {
        var apiUrl = Environment.GetEnvironmentVariable("N8N_API_URL");
        var apiKey = Environment.GetEnvironmentVariable("N8N_API_KEY");

        if (string.IsNullOrEmpty(apiUrl))
            throw new InvalidOperationException("N8N_API_URL environment variable is not set.");

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("N8N_API_KEY environment variable is not set.");

        _log.Info($"Fetching workflows from N8N at {apiUrl} with key {apiKey.Substring(0, 8)}...");

        var http = new HttpClient();
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("accept", "application/json");
        http.DefaultRequestHeaders.Add("X-N8N-API-KEY", apiKey);

        // --- 1. Get workflow list ---
        var listResp = await http.GetAsync(apiUrl, cancellationToken);
        listResp.EnsureSuccessStatusCode();

        await using var listStream = await listResp.Content.ReadAsStreamAsync(cancellationToken);
        var workflows = await JsonSerializer.DeserializeAsync<JsonElement>(listStream, cancellationToken: cancellationToken);
        var data = workflows.GetProperty("data");

        var jsonPieces = new List<string>();

        // 2. For each workflow, grab its full JSON and add running status
        foreach (var wf in data.EnumerateArray())
        {
            var id = wf.GetProperty("id").GetString();

            var wfResp = await http.GetAsync($"{apiUrl}/{id}", cancellationToken);
            wfResp.EnsureSuccessStatusCode();

            var wfJson = await wfResp.Content.ReadAsStringAsync(cancellationToken);

            // Parse the JSON, add isRunning field, and serialize back
            var wfDoc = JsonDocument.Parse(wfJson);
            var isRunning = _animationsManager.IsWorkflowRunning(id);

            // Build a new JSON object with the isRunning field
            using var stream = new System.IO.MemoryStream();
            await using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                // Copy all existing properties
                foreach (var property in wfDoc.RootElement.EnumerateObject())
                {
                    property.WriteTo(writer);
                }

                // Add isRunning property
                writer.WriteBoolean("isRunning", isRunning);
                writer.WriteString("apiUrl", apiUrl);

                writer.WriteEndObject();
            }

            var modifiedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            jsonPieces.Add(modifiedJson);
        }

        // 3. Manually build a JSON array of those workflow JSON objects
        var json = "[" + string.Join(",", jsonPieces) + "]";

        return Content(json, "application/json");
    }

    [SwaggerOperation("WorkflowsControl")]
    [HttpPost("workflows")]
    public async Task<IActionResult> WorkflowsControl([FromBody] WorkflowControl workflowControl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(workflowControl.Id))
            return BadRequest("Workflow ID is required");

        if (string.IsNullOrEmpty(workflowControl.WebhookUrl))
            return BadRequest("Webhook URL is required");

        if (string.IsNullOrEmpty(workflowControl.Schedule))
            return BadRequest("Schedule is required");

        try
        {
            await _animationsManager.StartWorkflowJob(workflowControl.Id, workflowControl.WebhookUrl, workflowControl.Schedule, cancellationToken);
            return Ok(new { message = $"Workflow {workflowControl.Id} scheduled successfully", workflowId = workflowControl.Id, webhookUrl = workflowControl.WebhookUrl, schedule = workflowControl.Schedule });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [SwaggerOperation("WorkflowsStop")]
    [HttpPost("workflows/stop")]
    public async Task<IActionResult> WorkflowsStop([FromBody] WorkflowStop workflowStop, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(workflowStop.Id))
            return BadRequest("Workflow ID is required");

        try
        {
            await _animationsManager.StopWorkflowJob(workflowStop.Id);
            return Ok(new { message = $"Workflow {workflowStop.Id} stopped successfully", workflowId = workflowStop.Id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public class WorkflowControl
    {
        public string Id { get; set; }
        public string WebhookUrl { get; set; }
        public string Schedule { get; set; }
    }

    public class WorkflowStop
    {
        public string Id { get; set; }
    }
}
