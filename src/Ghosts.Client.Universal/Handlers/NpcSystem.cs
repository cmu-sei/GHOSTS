// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

public class NpcSystem(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        _log.Trace($"Handling NpcSystem call: {this.Handler}");

        foreach (var timelineEvent in this.Handler.TimeLineEvents.Where(timelineEvent =>
                     !string.IsNullOrEmpty(timelineEvent.Command)))
        {
            Timeline t;

            switch (timelineEvent.Command.ToLower())
            {
                case "start":
                    t = TimelineBuilder.GetTimeline();
                    t.Status = Timeline.TimelineStatus.Run;
                    TimelineBuilder.SetLocalTimeline(t);
                    break;
                case "stop":
                    if (this.Timeline.Id != Guid.Empty)
                    {
                        var o = new Orchestrator();
                        o.StopTimeline(this.Timeline.Id);
                    }
                    else
                    {
                        t = TimelineBuilder.GetTimeline();
                        t.Status = Timeline.TimelineStatus.Stop;
                        StartupTasks.CleanupProcesses();
                        TimelineBuilder.SetLocalTimeline(t);
                    }

                    break;
                case "npc-scenario-action":
                    // Scenario-driven event: honor its scheduled offset, then report so the
                    // action is observable in the execution flow rather than silently dropped.
                    if (timelineEvent.DelayBeforeActual > 0 && this.Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual))
                        this.Token.ThrowIfCancellationRequested();

                    Report(new ReportItem
                    {
                        Handler = HandlerType.NpcSystem.ToString(),
                        Command = timelineEvent.Command,
                        Arg = string.Join(",", timelineEvent.CommandArgs),
                        Result = this.Handler.Initial
                    });
                    break;
            }
        }

        return Task.CompletedTask;
    }
}
