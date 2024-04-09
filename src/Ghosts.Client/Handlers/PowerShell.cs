using System;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json.Linq;

namespace Ghosts.Client.Handlers
{
    public class PowerShell : BaseHandler
    {
        public int Executionprobability = 100;
        public int Jitterfactor { get; set; } = 0;
        public PowerShell(TimelineHandler handler)
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
                Log.Trace($"PowerShell had a ThreadAbortException: {e}");
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
                int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out Executionprobability);
                if (Executionprobability < 0 || Executionprobability > 100) Executionprobability = 100;
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                Jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                Log.Trace($"PowerShell: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        while (true)
                        {
                            if (Executionprobability < _random.Next(0, 100))
                            {
                                //skipping this command
                                Log.Trace($"PowerShell Command choice skipped due to execution probability");
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, Jitterfactor));
                                continue;
                            }
                            var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                            {
                                this.Command(handler, timelineEvent, cmd.ToString());
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, Jitterfactor));
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
            var replacements = handler.HandlerArgs["replace"];

            foreach (var replacement in (JArray)replacements)
            {
                foreach (var o in replacement)
                {
                    command = Regex.Replace(command, "{" + ((JProperty)o).Name.ToString() + "}", ((Newtonsoft.Json.Linq.JArray)((JProperty)o).Value).PickRandom().ToString());
                }

            }

            var results = Command(command);
            Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = command, Trackable = timelineEvent.TrackableId, Result = results });
        }

        public static string Command(string command)
        {
            Log.Trace($"Spawning powershell.exe with command {command}");
            var processStartInfo = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using var process = Process.Start(processStartInfo);
            var outputString = string.Empty;
            Thread.Sleep(1000);

            if (process != null)
            {
                process.StandardInput.WriteLine(command);
                process.StandardInput.Close();
                outputString = process.StandardOutput.ReadToEnd();
                process.Close();
            }

            return outputString;
        }
    }
}