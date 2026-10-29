// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Controllers.Api;
using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Ghosts.Api.Tests;

/// <summary>A timeline item reaches the run screen with its extras, so White Cell sees a decision point's choices.</summary>
public class ExecutionTimelineItemDtoTests
{
    [Fact]
    public async Task Timeline_items_carry_their_extras()
    {
        const string extras = """{"choices":[{"id":"cafe","label":"Meet at the cafe"}]}""";
        var service = new Mock<IExecutionService>();
        service.Setup(s => s.GetTimelineItemsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExecutionTimelineItem { Id = 5, ExecutionId = 1, Number = 3, Extras = extras }]);

        var result = await new ExecutionsController(service.Object, null!, NullLogger<ExecutionsController>.Instance).GetTimelineItems(1, default);

        var item = Assert.Single(Assert.IsAssignableFrom<IEnumerable<ExecutionTimelineItemDto>>(Assert.IsType<OkObjectResult>(result.Result).Value));
        Assert.Equal(extras, item.Extras);
    }
}
