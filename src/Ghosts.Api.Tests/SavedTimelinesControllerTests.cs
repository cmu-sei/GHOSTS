// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Controllers.Api;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Ghosts.Api.Tests;

/// <summary>
/// The timeline editor's library lives in saved_timelines, so every browser sees the same timelines.
/// </summary>
public class SavedTimelinesControllerTests
{
    // Written as the editor writes it; 24:00:00 would not survive a trip through the domain's TimeSpan converter
    private const string Handlers = """
        [{"handlerType":"BrowserFirefox","initial":"about:blank","utcTimeOn":"00:00:00","utcTimeOff":"24:00:00","loop":true,
          "handlerArgs":{"stickiness":75},"timeLineEvents":[{"command":"browse","commandArgs":["https://example.com"],"delayAfter":1000}]}]
        """;

    [Fact]
    public async Task Create_StoresTheTimelineAndKeepsItsHandlersAsSent()
    {
        await using var context = TestDb.New();
        var controller = new SavedTimelinesController(context);

        var created = Value(await controller.Create(new SavedTimelineRequest("Browse", JArray.Parse(Handlers)), CancellationToken.None));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Browse", created.Name);
        Assert.True(JToken.DeepEquals(JArray.Parse(Handlers), created.TimeLineHandlers));
        var fetched = Value(await new SavedTimelinesController(context).Get(created.Id, CancellationToken.None));
        Assert.True(JToken.DeepEquals(JArray.Parse(Handlers), fetched.TimeLineHandlers));
        Assert.Equal("24:00:00", fetched.TimeLineHandlers[0]!["utcTimeOff"]!.Value<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_RejectsATimelineWithNoName(string? name)
    {
        await using var context = TestDb.New();

        var result = await new SavedTimelinesController(context).Create(new SavedTimelineRequest(name!, JArray.Parse(Handlers)), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.SavedTimelines);
    }

    [Fact]
    public async Task Create_RejectsATimelineWithNoHandlers()
    {
        await using var context = TestDb.New();

        var result = await new SavedTimelinesController(context).Create(new SavedTimelineRequest("Browse", null!), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.SavedTimelines);
    }

    [Fact]
    public async Task GetAll_ReturnsEveryTimelineByName()
    {
        await using var context = TestDb.New();
        var controller = new SavedTimelinesController(context);
        await controller.Create(new SavedTimelineRequest("Zeta", new JArray()), CancellationToken.None);
        await controller.Create(new SavedTimelineRequest("Alpha", JArray.Parse(Handlers)), CancellationToken.None);

        var all = Assert.IsAssignableFrom<IEnumerable<SavedTimelineViewModel>>(Assert.IsType<OkObjectResult>(await controller.GetAll(CancellationToken.None)).Value).ToList();

        Assert.Equal(["Alpha", "Zeta"], all.Select(t => t.Name));
        Assert.Single(all[0].TimeLineHandlers);
    }

    [Fact]
    public async Task Update_ReplacesNameAndHandlers()
    {
        await using var context = TestDb.New();
        var controller = new SavedTimelinesController(context);
        var created = Value(await controller.Create(new SavedTimelineRequest("Browse", JArray.Parse(Handlers)), CancellationToken.None));

        var updated = Value(await controller.Update(created.Id, new SavedTimelineRequest("Browse more", new JArray()), CancellationToken.None));

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Browse more", updated.Name);
        Assert.Empty(updated.TimeLineHandlers);
        Assert.Equal(created.CreatedUtc, updated.CreatedUtc);
        Assert.True(updated.UpdatedUtc >= created.UpdatedUtc);
        Assert.Equal("Browse more", Assert.Single(context.SavedTimelines).Name);
    }

    [Fact]
    public async Task Delete_RemovesTheTimeline()
    {
        await using var context = TestDb.New();
        var controller = new SavedTimelinesController(context);
        var created = Value(await controller.Create(new SavedTimelineRequest("Browse", JArray.Parse(Handlers)), CancellationToken.None));

        Assert.IsType<NoContentResult>(await controller.Delete(created.Id, CancellationToken.None));

        Assert.Empty(context.SavedTimelines);
    }

    [Fact]
    public async Task UnknownId_IsNotFound()
    {
        await using var context = TestDb.New();
        var controller = new SavedTimelinesController(context);
        var id = Guid.NewGuid();

        Assert.IsType<NotFoundResult>(await controller.Get(id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Update(id, new SavedTimelineRequest("Browse", new JArray()), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Delete(id, CancellationToken.None));
    }

    private static SavedTimelineViewModel Value(IActionResult result) =>
        Assert.IsType<SavedTimelineViewModel>(Assert.IsAssignableFrom<ObjectResult>(result).Value);
}
