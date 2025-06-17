// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using Ghosts.Client.Infrastructure;
using Ghosts.Client.TimelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Handlers;

public class NpcSystem : BaseHandler
{
    public NpcSystem(Timeline timeline, TimelineHandler handler)
    {
        Log.Trace($"Handling NpcSystem call: {handler}");

        foreach (var timelineEvent in handler.TimeLineEvents)
        {
            if (string.IsNullOrEmpty(timelineEvent.Command))
                continue;

            Timeline t;

            switch (timelineEvent.Command.ToLower())
            {
                case "start":
                    t = TimelineBuilder.GetTimeline();
                    t.Status = Timeline.TimelineStatus.Run;
                    TimelineBuilder.SetLocalTimeline(t);
                    break;
                case "stop":
                    if (timeline.Id != Guid.Empty)
                    {
                        var o = new Orchestrator();
                        o.StopTimeline(timeline.Id);
                    }
                    else
                    {
                        t = TimelineBuilder.GetTimeline();
                        t.Status = Timeline.TimelineStatus.Stop;
                        StartupTasks.CleanupProcesses();
                        TimelineBuilder.SetLocalTimeline(t);
                    }

                    break;
            }
        }
    }
}