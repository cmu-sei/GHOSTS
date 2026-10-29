// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Animations.AnimationDefinitions;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;

namespace Ghosts.Api.Tests;

/// <summary>
/// The bundled webhooks are GET, so a run's id travels in the query string: the scheduler names the execution and
/// item, and the job appends that to whatever webhook URL n8n reports at call time.
/// </summary>
public class WorkflowScopingTests
{
    [Fact]
    public void A_runs_webhook_query_names_its_execution_and_item()
    {
        var item = new ExecutionTimelineItem { Id = 42, ExecutionId = 7 };

        Assert.Equal("executionId=7&timelineItemId=42", ExecutionWorkflowScheduler.ExecutionQuery(item));
    }

    [Theory]
    [InlineData("http://n8n:5678/webhook/beliefs", "executionId=7", "http://n8n:5678/webhook/beliefs?executionId=7")]
    [InlineData("http://n8n:5678/webhook/beliefs?x=1", "executionId=7", "http://n8n:5678/webhook/beliefs?x=1&executionId=7")]
    [InlineData("http://n8n:5678/webhook/beliefs", null, "http://n8n:5678/webhook/beliefs")]
    [InlineData("http://n8n:5678/webhook/beliefs", "", "http://n8n:5678/webhook/beliefs")]
    public void The_query_is_appended_to_the_webhook_url(string url, string? query, string expected)
    {
        Assert.Equal(expected, WorkflowJob.AppendQuery(url, query));
    }
}
