// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Api.Infrastructure.Models;
using Ghosts.Api.ViewModels;
using Ghosts.Domain;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ghosts.Api.Tests;

/// <summary>
/// The machines page's "Run Timeline" body must bind to POST /api/timelines' MachineUpdateViewModel,
/// with the same Newtonsoft settings Program.cs gives MVC.
/// </summary>
public class RunTimelinePayloadTests
{
    private static readonly JsonSerializerSettings MvcSettings = new()
    {
        Converters = { new TimeSpanConverter(), new StringEnumConverter() }
    };

    [Fact]
    public void RunTimelineBody_BindsTheTimelineAsAPartialUpdate()
    {
        var machineId = Guid.NewGuid();
        var timelineId = Guid.NewGuid();
        // Same shape as machines-list.component.ts runTimeline()
        var body = $$"""
        {
          "machineId": "{{machineId}}",
          "type": "TimelinePartial",
          "update": {
            "id": "{{timelineId}}",
            "name": "Browse",
            "timeLineHandlers": [
              {
                "handlerType": "BrowserFirefox",
                "initial": "about:blank",
                "utcTimeOn": "00:00:00",
                "utcTimeOff": "24:00:00",
                "loop": true,
                "scheduleType": "Other",
                "handlerArgs": {},
                "timeLineEvents": [ { "command": "browse", "commandArgs": ["https://example.com"], "delayAfter": 1000 } ]
              }
            ]
          }
        }
        """;

        var vm = JsonConvert.DeserializeObject<MachineUpdateViewModel>(body, MvcSettings)!;
        var update = vm.ToMachineUpdate();

        Assert.Equal(machineId, update.MachineId);
        Assert.Equal(StatusType.Active, update.Status);
        Assert.NotNull(update.Update);
        Assert.Equal(UpdateClientConfig.UpdateType.TimelinePartial, update.Type);
        Assert.Equal(timelineId, update.Update.Id);
        var handler = Assert.Single(update.Update.TimeLineHandlers);
        Assert.Equal(HandlerType.BrowserFirefox, handler.HandlerType);
        Assert.Equal("browse", Assert.Single(handler.TimeLineEvents).Command);
    }

    [Fact]
    public void RequestFromClientBody_BindsARequestForTimeline()
    {
        var machineId = Guid.NewGuid();
        // Same shape as client-timelines-dialog.component.ts requestFromClient()
        var body = $$"""{ "machineId": "{{machineId}}", "type": "RequestForTimeline" }""";

        var update = JsonConvert.DeserializeObject<MachineUpdateViewModel>(body, MvcSettings)!.ToMachineUpdate();

        Assert.Equal(machineId, update.MachineId);
        Assert.Equal(StatusType.Active, update.Status);
        Assert.Equal(UpdateClientConfig.UpdateType.RequestForTimeline, update.Type);
        Assert.Null(update.Update);
    }
}
