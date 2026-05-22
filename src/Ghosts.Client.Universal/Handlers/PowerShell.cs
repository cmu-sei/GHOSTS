// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Text.RegularExpressions;
using System.Diagnostics;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json.Linq;

namespace Ghosts.Client.Universal.Handlers;

public class PowerShell(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    public int Executionprobability = 100;
    public int Jitterfactor { get; set; } = 0;

    protected override Task RunOnce()
    {
        if (this.Handler.HandlerArgs.ContainsKey("execution-probability"))
        {
            int.TryParse(this.Handler.HandlerArgs["execution-probability"].ToString(), out Executionprobability);
            if (Executionprobability < 0 || Executionprobability > 100) Executionprobability = 100;
        }

        if (this.Handler.HandlerArgs.ContainsKey("delay-jitter"))
        {
            Jitterfactor = Jitter.JitterFactorParse(this.Handler.HandlerArgs["delay-jitter"].ToString());
        }

        foreach (var timelineEvent in this.Handler.TimeLineEvents)
        {
            WorkingHours.Is(this.Handler);

            if (timelineEvent.DelayBeforeActual > 0) {
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) Token.ThrowIfCancellationRequested();
            }

            _log.Trace($"PowerShell: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

            switch (timelineEvent.Command)
            {
                case "random":
                    while (true)
                    {
                        if (Executionprobability < _random.Next(0, 100))
                        {
                            //skipping this command
                            _log.Trace($"PowerShell Command choice skipped due to execution probability");
                            if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, Jitterfactor))) Token.ThrowIfCancellationRequested();
                            continue;
                        }

                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            ProcessCommand(this.Handler, timelineEvent, cmd.ToString());
                        }
                        if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, Jitterfactor))) Token.ThrowIfCancellationRequested();
                    }
                default:
                    ProcessCommand(this.Handler, timelineEvent, timelineEvent.Command);

                    foreach (var cmd in timelineEvent.CommandArgs)
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                            ProcessCommand(this.Handler, timelineEvent, cmd.ToString());
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0) {
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayAfterActual)) Token.ThrowIfCancellationRequested();
            }
        }

        return Task.CompletedTask;
    }

    public void ProcessCommand(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        if (handler.HandlerArgs.ContainsKey("replace")) 
        {
            var replacements = handler.HandlerArgs["replace"];

            foreach (var replacement in JArray.FromObject(replacements))
            {
                foreach (var o in replacement)
                {
                    command = Regex.Replace(command, "{" + ((JProperty)o).Name.ToString() + "}",
                        ((Newtonsoft.Json.Linq.JArray)((JProperty)o).Value).PickRandom().ToString());
                }
            }
        }

        var results = ProcessCommand(command, Token);
        Report(new ReportItem
        {
            Handler = handler.HandlerType.ToString(),
            Command = command,
            Trackable = timelineEvent.TrackableId,
            Result = results
        });
    }

    public static string ProcessCommand(string command, CancellationToken token)
    {
        var pshell = "powershell.exe";
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
            pshell = "pwsh";
        }
        _log.Trace($"Spawning {pshell} with command {command}");
        var processStartInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false
        };

        using var process = Process.Start(processStartInfo);
        var outputString = string.Empty;
         if (token.WaitHandle.WaitOne(1000)) token.ThrowIfCancellationRequested();

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
