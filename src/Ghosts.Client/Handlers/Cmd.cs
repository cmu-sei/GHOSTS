// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;

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

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                Log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        while (true)
                        {
                            if (executionprobability < _random.Next(0, 100))
                            {
                                //skipping this command
                                Log.Trace($"Command choice skipped due to execution probability");
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                                continue;
                            }
                            var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                            {
                                this.Command(handler, timelineEvent, cmd.ToString());
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                        }
                    default:
                        this.Command(handler, timelineEvent, timelineEvent.Command);

                        foreach (var cmd in timelineEvent.CommandArgs)
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                                this.Command(handler, timelineEvent, cmd.ToString());
                        break;
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(timelineEvent.DelayAfterActual);
            }
        }

        public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
        {
            var results = Command(command);
            Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = command, Trackable = timelineEvent.TrackableId, Result = results });
        }

        public static string Command(string command)
        {
            Log.Trace($"Spawning cmd.exe with command {command}");

            var processStartInfo = new ProcessStartInfo("cmd", "/c " + command);
            processStartInfo.RedirectStandardOutput = true;
            processStartInfo.UseShellExecute = false;
            processStartInfo.CreateNoWindow = false;
            
            var process = new Process();
            process.StartInfo = processStartInfo;
            process.Start();
            
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            // Console.Write(output);
            Thread.Sleep(1000);
            
            return output;
        }
    }
}