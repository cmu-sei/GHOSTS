// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using WorkingHours = Ghosts.Client.Infrastructure.WorkingHours;

namespace Ghosts.Client.Handlers
{
    public class Cmd : BaseHandler
    {
        public int executionprobability = 100;
        public int jitterfactor { get; set; } = 0;  //used with Jitter.JitterFactorDelay
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
            catch (ThreadAbortException e)
            {
                Log.Trace($"Cmd had a ThreadAbortException: {e}");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {

            if (handler.HandlerArgs.ContainsKey("execution-probability"))
            {
                int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out executionprobability);
                if (executionprobability < 0 || executionprobability > 100) executionprobability = 100;
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }
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
                            if (executionprobability < _random.Next(0, 100))
                            {
                                //skipping this command
                                Log.Trace($"Command choice skipped due to execution probability");
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor));
                                continue;
                            }
                            var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                            {
                                this.Command(handler, timelineEvent, cmd.ToString());
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor));
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
            Log.Trace($"Spawning cmd.exe with command {command}");
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
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = command, Trackable = timelineEvent.TrackableId, Result = outputString});
                    process.Close();
                }
            }
        }
    }
}