// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Handlers;

public class Print : BaseHandler
{
    public Print(TimelineHandler handler)
    {
        try
        {
            base.Init(handler);
            Log.Trace("Spawning printer job...");

            if (handler.Loop)
            {
                while (true)
                {
                    Ex(handler);
                }
            }
            else
            {
                Ex(handler);
            }
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Ex(TimelineHandler handler)
    {
        foreach (var timelineEvent in handler.TimeLineEvents)
        {
            Infrastructure.WorkingHours.Is(handler);

            if (timelineEvent.DelayBefore > 0)
            {
                Thread.Sleep(timelineEvent.DelayBefore);
            }

            Log.Trace($"Print Job: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

            Command(handler, timelineEvent, timelineEvent.Command);

            if (timelineEvent.DelayAfter > 0)
            {
                Thread.Sleep(timelineEvent.DelayAfter);
            }
        }
    }

    public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        Thread.Sleep(1000);

        try
        {
            foreach (var fileToPrint in timelineEvent.CommandArgs)
            {
                var info = new ProcessStartInfo();
                info.Verb = "print";
                info.FileName = fileToPrint.ToString();
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;

                var process = new Process();
                process.StartInfo = info;
                process.Start();

                try
                {
                    process.WaitForInputIdle();
                    Thread.Sleep(3000);
                    if (false == process.CloseMainWindow())
                    {
                        process.SafeKill();
                    }
                }
                catch
                {
                    //
                }

                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = command, Trackable = timelineEvent.TrackableId });
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception);
        }
    }
}