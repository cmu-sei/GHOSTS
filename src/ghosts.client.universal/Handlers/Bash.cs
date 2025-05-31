// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

public class Bash(Timeline timeline, TimelineHandler handler, CancellationToken token)
    : BaseHandler(timeline, handler, token)
{
    public int executionprobability = 100;
    public int jitterfactor { get; set; } = 0; //used with Jitter.JitterFactorDelay

    protected override Task RunOnce()
    {
        if (this.Handler.HandlerArgs.TryGetValue("execution-probability", out var v1))
        {
            int.TryParse(v1.ToString(), out executionprobability);
            if (executionprobability < 0 || executionprobability > 100) executionprobability = 100;
        }

        if (this.Handler.HandlerArgs.TryGetValue("delay-jitter", out var v2))
        {
            jitterfactor = Jitter.JitterFactorParse(v2.ToString());
        }

        foreach (var timelineEvent in this.Handler.TimeLineEvents)
        {
            WorkingHours.Is(this.Handler);

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            _log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    while (true)
                    {
                        if (executionprobability < _random.Next(0, 100))
                        {
                            //skipping this command
                            _log.Trace($"Command choice skipped due to execution probability");
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                            continue;
                        }

                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            ProcessCommand(this.Handler.Initial, cmd.ToString());
                        }

                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                    }
                default:

                    ProcessCommand(this.Handler.Initial, timelineEvent.Command);

                    foreach (var cmd in timelineEvent.CommandArgs.Where(cmd => !string.IsNullOrEmpty(cmd.ToString())))
                    {
                        ProcessCommand(this.Handler.Initial, cmd.ToString());
                    }

                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
                Thread.Sleep(timelineEvent.DelayAfterActual);
        }

        return Task.CompletedTask;
    }

    private void ProcessCommand(string initial, string rawCommand)
    {
        this.Command = rawCommand.Replace("\"", "\\\"");

        var p = new Process();
        //p.EnableRaisingEvents = false;
        p.StartInfo.FileName = string.IsNullOrEmpty(initial) ? "bash" : initial;
        p.StartInfo.Arguments = $"-c \"{this.Command}\"";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        //* Set your output and error (asynchronous) handlers
        p.OutputDataReceived += OutputHandler;
        p.ErrorDataReceived += ErrorHandler;
        p.StartInfo.CreateNoWindow = true;
        _log.Trace($"Spawning {p.StartInfo.FileName} with command {this.Command}");
        p.Start();

        while (!p.StandardOutput.EndOfStream)
        {
            this.Result += p.StandardOutput.ReadToEnd();
        }

        p.WaitForExit();
        Report(new ReportItem { Handler = HandlerType.Command.ToString(), Command = this.Command, Result = Result });
    }

    private void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        Result += outLine.Data;
    }

    private static void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
    {
        //* Do your stuff with the output (write to console/log/StringBuilder)
        Console.WriteLine(outLine.Data);
    }
}
