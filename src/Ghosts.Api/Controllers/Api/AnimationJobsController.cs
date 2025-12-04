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
using Swashbuckle.AspNetCore.Annotations;

namespace Ghosts.Api.Controllers.Api;

[Route("api/animations")]
[Produces("application/json")]
public class AnimationJobsController(IServiceProvider serviceProvider) : Controller
{
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

        // 2. For each workflow, grab its full JSON and stash the raw string
        foreach (var wf in data.EnumerateArray())
        {
            var id = wf.GetProperty("id").GetString();

            var wfResp = await http.GetAsync($"{apiUrl}/{id}", cancellationToken);
            wfResp.EnsureSuccessStatusCode();

            var wfJson = await wfResp.Content.ReadAsStringAsync(cancellationToken);

            // wfJson is already valid JSON; do NOT parse it, just collect it
            jsonPieces.Add(wfJson);
        }

        // 3. Manually build a JSON array of those workflow JSON objects
        var json = "[" + string.Join(",", jsonPieces) + "]";

        return Content(json, "application/json");
    }
}
