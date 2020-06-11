// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Infrastructure;
using Ghosts.Client.TimelineManager;
using Ghosts.Domain;
using NLog;

namespace Ghosts.Client.Handlers
{
    public class NpcSystem : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public NpcSystem(TimelineHandler handler)
        {
            _log.Trace($"Handling NpcSystem call: {handler}");

            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                if (string.IsNullOrEmpty(timelineEvent.Command))
                    continue;

                Timeline timeline;

                switch (timelineEvent.Command.ToLower())
                {
                    case "start":
                        timeline = TimelineBuilder.GetLocalTimeline();
                        timeline.Status = Timeline.TimelineStatus.Run;
                        TimelineBuilder.SetLocalTimeline(timeline);
                        break;
                    case "stop":
                        timeline = TimelineBuilder.GetLocalTimeline();
                        timeline.Status = Timeline.TimelineStatus.Stop;

                        StartupTasks.CleanupProcesses();

                        TimelineBuilder.SetLocalTimeline(timeline);
                        break;
                }
            }
        }

    }
}