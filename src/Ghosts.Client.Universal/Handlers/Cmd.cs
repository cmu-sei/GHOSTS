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
    protected override Task RunOnce()
    {
        var handler = this.Handler;

        if (handler.HandlerArgs.ContainsKey("execution-probability"))
        {
            int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out this.ExecutionProbability);
            if (this.ExecutionProbability < 0 || this.ExecutionProbability > 100) this.ExecutionProbability = 100;
        }

        if (handler.HandlerArgs.ContainsKey("delay-jitter"))
        {
            this.JitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
        }

        foreach (var timelineEvent in handler.TimeLineEvents)
        {
            WorkingHours.Is(handler);

            if (timelineEvent.DelayBeforeActual > 0) {
                    if (Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) Token.ThrowIfCancellationRequested();
            }

            _log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    while (true)
                    {
                        if (this.ExecutionProbability < _random.Next(0, 100))
                        {
                            //skipping this command
                            _log.Trace($"Command choice skipped due to execution probability");
                            if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, this.JitterFactor))) Token.ThrowIfCancellationRequested();
                            continue;
                        }

                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            ProcessCommand(handler, timelineEvent, cmd.ToString());
                        }
                        if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, this.JitterFactor))) Token.ThrowIfCancellationRequested();
                    }
                default:
                    ProcessCommand(handler, timelineEvent, timelineEvent.Command);

                    foreach (var cmd in timelineEvent.CommandArgs)
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                            ProcessCommand(handler, timelineEvent, cmd.ToString());
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayAfterActual)) Token.ThrowIfCancellationRequested();
        }

        return Task.CompletedTask;
    }

    public void ProcessCommand(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
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
        if (token.WaitHandle.WaitOne(1000)) token.ThrowIfCancellationRequested();

        return output;
    }
}
