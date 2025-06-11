// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Universal.Handlers;

public class Aws(Timeline timeline, TimelineHandler handler, CancellationToken token)
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
        this.Command = $"{rawCommand} --no-verify";

        try
        {
            var p = new Process
            {
                EnableRaisingEvents = false,
                StartInfo =
                {
                    FileName = "aws",
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

            err = err.RemoveTextBetweenMarkers("urllib3/connectionpool", "#ssl-warnings");
            if (err.Length > 0)
            {
                _log.Error($"{err} on {this.Command}");
            }

            Report(new ReportItem { Handler = nameof(HandlerType.Aws), Command = this.Command, Result = this.Result });

        }
        catch (Exception exc)
        {
            _log.Debug(exc);
        }
    }
}
