﻿// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;

namespace ghosts.client.universal.handlers
{
    public class Aws : BaseHandler
    {
        private string Result { get; set; }
        private readonly TimelineHandler _handler;

        public Aws(TimelineHandler handler)
        {
            _handler = handler;

            try
            {
                if (_handler.Loop)
                {
                    while (true)
                    {
                        Ex();
                    }
                }

                Ex();
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void Ex()
        {
            var handlerArgs = BuildHandlerArgVariables.BuildHandlerArgs(_handler);
            foreach (var timelineEvent in _handler.TimeLineEvents)
            {
                WorkingHours.Is(_handler);

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
                                Command(cmd);
                            }
                        }

                        break;
                }
                if (timelineEvent.DelayAfterActual <= 0) continue;
                Thread.Sleep(timelineEvent.DelayAfterActual);
            }
        }

        private void Command(string command)
        {
            Result = string.Empty;

            command = $"{command} --no-verify";

            try
            {
                var p = new Process
                {
                    EnableRaisingEvents = false,
                    StartInfo =
                    {
                        FileName = "aws",
                        Arguments = command,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();

                while (!p.StandardOutput.EndOfStream)
                {
                    Result += p.StandardOutput.ReadToEnd();
                }

                var err = string.Empty;
                while (!p.StandardError.EndOfStream)
                {
                    err += p.StandardError.ReadToEnd();
                }
                err = err.RemoveTextBetweenMarkers("urllib3/connectionpool", "#ssl-warnings");
                if (err.Length > 0)
                {
                    _log.Error($"{err} on {command}");
                }

                Report(new ReportItem { Handler = HandlerType.Aws.ToString(), Command = command, Result = Result });
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
            }
        }
    }
}
