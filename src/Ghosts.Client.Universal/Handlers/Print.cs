// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Universal.Handlers;

public class Print(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        foreach (var timelineEvent in this.Handler.TimeLineEvents)
        {
            WorkingHours.Is(this.Handler);

            if (timelineEvent.DelayBeforeActual > 0)
            {
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) Token.ThrowIfCancellationRequested();
            }

            _log.Trace($"Print Job: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            ProcessCommand(this.Handler, timelineEvent, timelineEvent.Command);

            if (timelineEvent.DelayAfterActual > 0)
            {
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayAfterActual)) Token.ThrowIfCancellationRequested();
            }
        }

        return Task.CompletedTask;
    }

    public void ProcessCommand(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        if (Token.WaitHandle.WaitOne(1000)) Token.ThrowIfCancellationRequested();

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
                    if (Token.WaitHandle.WaitOne(3000)) Token.ThrowIfCancellationRequested();
                    if (false == process.CloseMainWindow())
                    {
                        process.SafeKill();
                    }
                }
                catch (Exception e)
                {
                    if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
                    {
                        throw;
                    }
                    // ignore
                }

                Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = command, Trackable = timelineEvent.TrackableId });
            }
        }
        catch (Exception e)
        {
            if (e is ThreadAbortException || e is ThreadInterruptedException || e is OperationCanceledException)
            {
                throw;
            }
            _log.Error(e);
        }
    }
}
