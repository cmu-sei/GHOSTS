// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Handlers
{
    public class Reboot : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public Reboot(TimelineHandler handler)
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                    Thread.Sleep(timelineEvent.DelayBefore);

                _log.Trace($"Reboot: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    default:
                        System.Diagnostics.Process.Start("shutdown.exe", "-r -t 0");
                        break;
                }
            }
        }
    }
}