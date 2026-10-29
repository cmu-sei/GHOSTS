// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>White Cell's endpoints say whose problem a failure is: 404 no such run or item, 409 a decision that does not fit the item.</summary>
public class ExecutionStateControllerTests
{
    private readonly Mock<IExecutionStateService> _service = new();

    [Fact]
    public async Task An_unknown_run_is_not_found()
    {
        _service.Setup(s => s.GetAsync(9, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Execution not found"));

        var result = await new ExecutionStateController(_service.Object).Get(9, default);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task A_decision_for_an_item_without_choices_is_a_conflict()
    {
        _service.Setup(s => s.RecordDecisionAsync(1, 5, It.IsAny<RecordDecisionDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Timeline item #3 is not a decision point."));

        var result = await new ExecutionStateController(_service.Object).RecordDecision(1, 5, new RecordDecisionDto("cafe", "white-cell"), default);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }
}
