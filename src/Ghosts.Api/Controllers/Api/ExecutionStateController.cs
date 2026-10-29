// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghosts.Api.Controllers.Api;

/// <summary>
/// A run's ground truth, for White Cell: the exercise clock, flags and facts, objectives met, and decisions recorded.
/// Triggered timeline items come due when their condition holds against this state.
/// </summary>
[ApiController]
[Route("api/executions/{executionId:int}")]
public class ExecutionStateController(IExecutionStateService service) : ControllerBase
{
    /// <summary>The run's state: clock, flags, facts, objectives and decisions.</summary>
    [HttpGet("state")]
    public Task<ActionResult<ExecutionStateDto>> Get(int executionId, CancellationToken ct) =>
        Handle(() => service.GetAsync(executionId, ct));

    /// <summary>Set or clear flags by hand. Body: { "set": ["coffee-ready"], "clear": [], "by": "white-cell" }</summary>
    [HttpPost("state/flags")]
    public Task<ActionResult<ExecutionStateDto>> SetFlags(int executionId, [FromBody] SetFlagsDto dto, CancellationToken ct) =>
        Handle(() => service.SetFlagsAsync(executionId, dto, ct));

    /// <summary>Record the answer a decision point got: its effects apply and the item completes. Body: { "choiceId": "cafe", "by": "white-cell" }</summary>
    [HttpPost("timeline-items/{itemId:int}/decision")]
    public Task<ActionResult<ExecutionStateDto>> RecordDecision(int executionId, int itemId, [FromBody] RecordDecisionDto dto, CancellationToken ct) =>
        Handle(() => service.RecordDecisionAsync(executionId, itemId, dto, ct));

    /// <summary>Mark whether an item's expected response was met. Body: { "met": true, "notes": "Refilled in six minutes", "by": "white-cell" }</summary>
    [HttpPost("timeline-items/{itemId:int}/response")]
    public Task<ActionResult<ExecutionStateDto>> MarkResponse(int executionId, int itemId, [FromBody] MarkResponseDto dto, CancellationToken ct) =>
        Handle(() => service.MarkResponseAsync(executionId, itemId, dto, ct));

    private async Task<ActionResult<ExecutionStateDto>> Handle(Func<Task<ExecutionStateDto>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
