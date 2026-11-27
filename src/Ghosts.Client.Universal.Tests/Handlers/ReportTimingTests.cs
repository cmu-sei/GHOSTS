// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using Xunit;

namespace Ghosts.Client.Universal.Tests.Handlers;

public class ReportTimingTests
{
    // The TIMELINE line a handler reports carries when the action started, when it finished and the process that ran it.
    [Fact]
    public void Report_WritesStartFinishAndProcessId()
    {
        var target = new MemoryTarget("report-timing") { Layout = "${message}" };
        var config = LogManager.Configuration ?? new LoggingConfiguration();
        config.AddRuleForAllLevels(target, "TIMELINE");
        LogManager.Configuration = config;

        var trackable = Guid.NewGuid().ToString();
        var started = DateTime.UtcNow.AddSeconds(-2);
        BaseHandler.Report(new ReportItem { Handler = "Command", Command = "dir", Trackable = trackable, StartedUtc = started, ProcessId = 4242 });

        var line = target.Logs.Single(l => l.Contains(trackable));
        var record = JsonConvert.DeserializeObject<TimeLineRecord>(line.Split('|', 3)[2]);

        Assert.Equal(started, record.StartedUtc);
        Assert.True(record.FinishedUtc >= started.AddSeconds(2));
        Assert.Equal(4242, record.ProcessId);
    }
}
