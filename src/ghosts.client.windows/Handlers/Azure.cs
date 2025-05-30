// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Handlers
{
    public class Azure : BaseHandler
    {
        private string Result { get; set; }
        private readonly TimelineHandler _handler;
        
        public Azure(TimelineHandler handler)
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
                Log.Error(e);
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
                                this.Command(cmd);
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
            this.Result = string.Empty;

            try
            {
                var p = new Process
                {
                    EnableRaisingEvents = false,
                    StartInfo =
                    {
                        FileName = "az",
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
                    this.Result += p.StandardOutput.ReadToEnd();
                }

                var err = string.Empty;
                while (!p.StandardError.EndOfStream)
                { 
                    err += p.StandardError.ReadToEnd();
                }
                if (err.Length > 0)
                {
                    Log.Error($"{err} on {command}");
                }
                
                Report(new ReportItem {Handler = HandlerType.Azure.ToString(), Command = command, Result = this.Result});
            }
            catch (Exception exc)
            {
                Log.Debug(exc);
            }
        }
    }
}
