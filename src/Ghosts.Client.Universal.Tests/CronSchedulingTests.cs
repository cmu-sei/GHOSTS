// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Newtonsoft.Json;
using Xunit;

namespace Ghosts.Client.Universal.Tests;

public class CronSchedulingTests
{
    private static TimelineHandler CreateHandler()
    {
        return new TimelineHandler
        {
            HandlerType = HandlerType.Bash,
            Loop = false,
            Schedule = "0/5 * * * * ?", // every 5 seconds
            ScheduleType = TimelineHandler.TimelineScheduleType.Cron,
            HandlerArgs = new Dictionary<string, object>(),
            TimeLineEvents = new List<TimelineEvent>
            {
                new() { Command = "ls", CommandArgs = new List<object>(), DelayAfter = 0, DelayBefore = 0 }
            }
        };
    }

    [Fact]
    public void GetTrigger_CreatesValidTrigger_FromHandler()
    {
        var handler = CreateHandler();
        var scheduling = new CronScheduling();

        var trigger = scheduling.GetTrigger(handler);

        Assert.NotNull(trigger);
        Assert.Equal(handler.HandlerType.ToString(), trigger.Key.Group);
    }

    [Fact]
    public void GetJob_CreatesValidJob_FromHandler()
    {
        var handler = CreateHandler();
        var scheduling = new CronScheduling();

        var job = scheduling.GetJob(handler);

        Assert.NotNull(job);
        Assert.Equal(handler.HandlerType.ToString(), job.Key.Group);
    }

    [Fact]
    public void GetJob_ContainsSerializedHandler_InDataMap()
    {
        var handler = CreateHandler();
        var scheduling = new CronScheduling();

        var job = scheduling.GetJob(handler);

        Assert.True(job.JobDataMap.ContainsKey("handler"));
        var serialized = job.JobDataMap["handler"].ToString();
        var deserialized = JsonConvert.DeserializeObject<TimelineHandler>(serialized);
        Assert.NotNull(deserialized);
        Assert.Equal(handler.HandlerType, deserialized.HandlerType);
        Assert.Equal(handler.Schedule, deserialized.Schedule);
    }
}
