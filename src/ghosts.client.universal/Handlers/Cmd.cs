// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

public class Cmd(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    private int _executionProbability = 100;
    private int _jitterFactor = 0; //used with Jitter.JitterFactorDelay

    protected override Task RunOnce()
    {
        var handler = this.Handler;

        if (handler.HandlerArgs.ContainsKey("execution-probability"))
        {
            int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out _executionProbability);
            if (_executionProbability < 0 || _executionProbability > 100) _executionProbability = 100;
        }

        if (handler.HandlerArgs.ContainsKey("delay-jitter"))
        {
            _jitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
        }

        foreach (var timelineEvent in handler.TimeLineEvents)
        {
            WorkingHours.Is(handler);

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            _log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    while (true)
                    {
                        if (_executionProbability < _random.Next(0, 100))
                        {
                            //skipping this command
                            _log.Trace($"Command choice skipped due to execution probability");
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                            continue;
                        }

                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            ProcessCommand(handler, timelineEvent, cmd.ToString());
                        }

                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                    }
                default:
                    ProcessCommand(handler, timelineEvent, timelineEvent.Command);

                    foreach (var cmd in timelineEvent.CommandArgs)
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                            ProcessCommand(handler, timelineEvent, cmd.ToString());
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
                Thread.Sleep(timelineEvent.DelayAfterActual);
        }

        return Task.CompletedTask;
    }

    public void ProcessCommand(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        var results = ProcessCommand(command);
        Report(new ReportItem
        {
            Handler = handler.HandlerType.ToString(),
            Command = command,
            Trackable = timelineEvent.TrackableId,
            Result = results
        });
    }

    public static string ProcessCommand(string command)
    {
        _log.Trace($"Spawning cmd.exe with command {command}");

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
