// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using ghosts.client.linux.Infrastructure;
using ghosts.client.linux.timelineManager;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace ghosts.client.linux.handlers
{
    public class NpcSystem : BaseHandler
    {
        public NpcSystem(Timeline timeline, TimelineHandler handler)
        {
            _log.Trace($"Handling NpcSystem call: {handler}");

            foreach (var timelineEvent in handler.TimeLineEvents.Where(timelineEvent => !string.IsNullOrEmpty(timelineEvent.Command)))
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
                        if (timeline.Id != Guid.Empty)
                        {
                            Orchestrator.StopTimeline(timeline.Id);
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
}
