// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;

namespace Ghosts.Client.Handlers;

public class Cmd : BaseHandler
{
    public Cmd(TimelineHandler handler)
    {
        try
        {
            base.Init(handler);
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
        catch (ThreadAbortException)
        {
            ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Command);
            Log.Trace("Cmd closing...");
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
            WorkingHours.Is(handler);

            if (timelineEvent.DelayBefore > 0)
                Thread.Sleep(timelineEvent.DelayBefore);

            Log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

            switch (timelineEvent.Command)
            {
                case "random":
                    while (true)
                    {
                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            this.Command(handler, timelineEvent, cmd.ToString());
                        }
                        Thread.Sleep(timelineEvent.DelayAfter);
                    }
                default:
                    this.Command(handler, timelineEvent, timelineEvent.Command);

                    foreach (var cmd in timelineEvent.CommandArgs)
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                            this.Command(handler, timelineEvent, cmd.ToString());
                    break;
            }

            if (timelineEvent.DelayAfter > 0)
                Thread.Sleep(timelineEvent.DelayAfter);
        }
    }

    public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        Log.Trace("Spawning cmd.exe...");
        var processStartInfo = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using (var process = Process.Start(processStartInfo))
        {
            Thread.Sleep(1000);
            if (process != null)
            {
                process.StandardInput.WriteLine(command);
                process.StandardInput.Close(); // line added to stop process from hanging on ReadToEnd()
                var outputString = process.StandardOutput.ReadToEnd();
                this.Report(handler.HandlerType.ToString(), command, outputString, timelineEvent.TrackableId);
            }
        }
    }
}