// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

public class Reboot(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        foreach (var timelineEvent in this.Handler.TimeLineEvents)
        {
            WorkingHours.Is(this.Handler);

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            _log.Trace($"Reboot: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS())
            {
                case (true, _, _):
                    Process.Start("shutdown.exe", "-r -t 0");
                    break;
                case (_, true, _):
                    Process.Start("shutdown", "-r now");
                    break;
                case (_, _, true):
                    Process.Start("sudo", "shutdown -r now");
                    break;
                default:
                    _log.Warn("Unsupported platform for reboot");
                    break;
            }

        }

        return Task.CompletedTask;
    }
}
