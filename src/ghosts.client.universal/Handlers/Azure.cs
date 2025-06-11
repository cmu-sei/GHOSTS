// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

public class Azure(Timeline timeline, TimelineHandler handler, CancellationToken token)
    : BaseHandler(timeline, handler, token)
{
    protected override Task RunOnce()
    {
        var handlerArgs = BuildHandlerArgVariables.BuildHandlerArgs(this.Handler);
        foreach (var timelineEvent in this.Handler.TimeLineEvents)
        {
            WorkingHours.Is(this.Handler);

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            switch (timelineEvent.Command)
            {
                default:
                    foreach (var cmdObj in timelineEvent.CommandArgs)
                    {
                        var cmd = cmdObj?.ToString();
                        if (!string.IsNullOrEmpty(cmd))
                        {
                            cmd = BuildHandlerArgVariables.ReplaceCommandVariables(cmd, handlerArgs);
                            ProcessCommand(cmd);
                        }
                    }

                    break;
            }

            if (timelineEvent.DelayAfterActual <= 0) continue;
            Thread.Sleep(timelineEvent.DelayAfterActual);
        }

        return Task.CompletedTask;
    }

    private void ProcessCommand(string rawCommand)
    {
        this.Command = rawCommand;

        try
        {
            var p = new Process
            {
                EnableRaisingEvents = false,
                StartInfo =
                {
                    FileName = "az",
                    Arguments = this.Command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            p.Start();

            while (!p.StandardOutput.EndOfStream)
            {
                this.Result += p.StandardOutput.ReadToEnd();
            }

            var err = string.Empty;
            while (!p.StandardError.EndOfStream)
            {
                err += p.StandardError.ReadToEnd();
            }

            if (err.Length > 0)
            {
                _log.Error($"{err} on {this.Command}");
            }

            Report(new ReportItem { Handler = nameof(HandlerType.Azure), Command = this.Command, Result = this.Result });
        }
        catch (Exception exc)
        {
            _log.Debug(exc);
        }
    }
}
