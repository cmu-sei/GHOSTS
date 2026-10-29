// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>The binding endpoints return the service's answer, and 404 for a run or NPC that is not there.</summary>
public class ExecutionBindingControllerTests
{
    private readonly Mock<IExecutionBindingService> _service = new();

    [Fact]
    public async Task Auto_binding_returns_the_counts()
    {
        _service.Setup(s => s.AutoBindAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new BindingResultDto(4, 1, 5));

        var result = await new ExecutionBindingController(_service.Object).AutoBind(1, default);

        Assert.Equal(4, Assert.IsType<BindingResultDto>(Assert.IsType<OkObjectResult>(result.Result).Value).Bound);
    }

    [Fact]
    public async Task Setting_a_host_for_an_npc_outside_the_run_is_not_found()
    {
        var npc = Guid.NewGuid();
        _service.Setup(s => s.SetHostAsync(1, npc, "desk-01", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NPC not found in this execution"));

        var result = await new ExecutionBindingController(_service.Object).SetHost(1, npc, new SetHostDto("desk-01"), default);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}
