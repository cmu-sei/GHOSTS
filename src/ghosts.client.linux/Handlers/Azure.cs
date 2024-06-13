// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace ghosts.client.linux.handlers
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
                _log.Error(e);
            }
        }

        private void Ex()
        {
            foreach (var timelineEvent in _handler.TimeLineEvents)
            {
                WorkingHours.Is(_handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                switch (timelineEvent.Command)
                {
                    default:
                        foreach (var cmd in timelineEvent.CommandArgs.Where(cmd => !string.IsNullOrEmpty(cmd.ToString())))
                        {
                            this.Command(cmd.ToString());
                        }

                        break;
                }
                if (timelineEvent.DelayAfterActual <= 0) continue;
                Thread.Sleep(timelineEvent.DelayAfterActual);
            }
        }

        private void Command(string command)
        {
            command = $"{command} --no-verify";
            
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
                        CreateNoWindow = true
                    }
                };
                p.Start();

                while (!p.StandardOutput.EndOfStream)
                {
                    this.Result += p.StandardOutput.ReadToEnd();
                }

                Report(new ReportItem {Handler = HandlerType.Azure.ToString(), Command = command, Result = this.Result});
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
            }
        }
    }
}
