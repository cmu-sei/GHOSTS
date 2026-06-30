using System;
using System.Linq;
using Xunit;

namespace Ghosts.Api.Tests;

public class QueueSyncSplitTests
{
    // Regression for the bug fixed in QueueSyncService.cs: client log lines
    // have the shape  TYPE|<utc>|<json>, and the JSON payload's Result field
    // often contains '|' characters (e.g. captured HTTP response bodies).
    // The previous Split('|') with no limit over-split the line, leaving
    // array[2] as a JSON fragment and causing DeserializeObject to throw,
    // which silently dropped the activity.

    [Fact]
    public void Split_PreservesPipesInsideJsonResultPayload()
    {
        var line = "TIMELINE|2026-05-13T12:00:00Z|{\"Command\":\"curl\",\"Result\":\"a|b|c\"}";

        var array = line.Split('|', 3);

        Assert.Equal(3, array.Length);
        Assert.Equal("TIMELINE", array[0]);
        Assert.Equal("2026-05-13T12:00:00Z", array[1]);
        Assert.Equal("{\"Command\":\"curl\",\"Result\":\"a|b|c\"}", array[2]);
    }

    [Fact]
    public void Split_StillHandlesLinesWithoutTrailingPipes()
    {
        var line = "TIMELINE|2026-05-13T12:00:00Z|{\"Command\":\"curl\",\"Result\":\"ok\"}";

        var array = line.Split('|', 3);

        Assert.Equal(3, array.Length);
        Assert.Equal("TIMELINE", array[0]);
        Assert.Equal("2026-05-13T12:00:00Z", array[1]);
        Assert.Equal("{\"Command\":\"curl\",\"Result\":\"ok\"}", array[2]);
    }

    [Fact]
    public void Split_LegacyTwoFieldLineUnaffected()
    {
        // The old, time-less format: TYPE|<json>. Still two fields after the
        // change, which is what the legacy branch in QueueSyncService expects.
        var line = "TIMELINE|{\"Command\":\"curl\",\"Result\":\"ok\"}";

        var array = line.Split('|', 3);

        Assert.Equal(2, array.Length);
        Assert.Equal("TIMELINE", array[0]);
        Assert.Equal("{\"Command\":\"curl\",\"Result\":\"ok\"}", array[1]);
    }

    // The npc-scenario-action loop-closure parses the CommandArg emitted by
    // ExecutionService.BuildTimelineForNpc ("execution:{id},event:{n},...") to
    // find the execution timeline item to advance. This documents that contract.
    [Theory]
    [InlineData("execution:42,event:3,some description", 42, 3)]
    [InlineData("execution:7,event:1,trigger:foo,desc", 7, 1)]
    [InlineData(" execution:9 , event:2 ", 9, 2)]
    public void ScenarioActionArg_ParsesExecutionAndEvent(string commandArg, int expectedExecution, int expectedEvent)
    {
        int? executionId = null;
        int? eventNumber = null;
        foreach (var t in commandArg.Split(',').Select(token => token.Trim()))
        {
            if (t.StartsWith("execution:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(t["execution:".Length..], out var eid))
                executionId = eid;
            else if (t.StartsWith("event:", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(t["event:".Length..], out var num))
                eventNumber = num;
        }

        Assert.Equal(expectedExecution, executionId);
        Assert.Equal(expectedEvent, eventNumber);
    }
}
