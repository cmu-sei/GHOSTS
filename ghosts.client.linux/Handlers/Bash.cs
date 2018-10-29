// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace ghosts.client.linux.handlers
{
    public class Bash : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public string Result { get; private set; }

        public Bash(TimelineHandler handler)
        {
            _log.Trace("Spawning Bash...");

            try
            {
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
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                    Thread.Sleep(timelineEvent.DelayBefore);

                _log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    default:

                        this.Command(timelineEvent.Command);

                        foreach (var cmd in timelineEvent.CommandArgs)
                            if (!string.IsNullOrEmpty(cmd))
                                this.Command(cmd);
                        break;
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(timelineEvent.DelayAfter);
            }
        }

        private void Command(string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"");

            var p = new Process();
            p.EnableRaisingEvents = false;
            p.StartInfo.FileName = "bash";
            p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = false;
            p.Start();

            while (!p.StandardOutput.EndOfStream)
            {
                this.Result += p.StandardOutput.ReadToEnd();
            }

            this.Report(HandlerType.Command.ToString(), escapedArgs, this.Result);
        }
    }
}