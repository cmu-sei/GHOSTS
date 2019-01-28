// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace ghosts.client.linux.handlers
{
    public class Curl : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public string Result { get; private set; }

        public Curl(TimelineHandler handler)
        {
            _log.Trace("Spawning Curl...");

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

                switch (timelineEvent.Command)
                {
                    default:
                        this.Command(timelineEvent.Command);

                        foreach (var cmd in timelineEvent.CommandArgs)
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                                this.Command(cmd.ToString());
                        break;
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(timelineEvent.DelayAfter);
            }
        }

        private void Command(string command)
        {
            try
            {
                var escapedArgs = command;//.Replace("\"", "\\\"");

                if (!escapedArgs.Contains("--user-agent ") && !escapedArgs.Contains("-A"))
                    escapedArgs += $" -A \"{UserAgentManager.Get()}\"";

                Console.WriteLine($"curl {escapedArgs}");

                var p = new Process();
                p.EnableRaisingEvents = false;
                p.StartInfo.FileName = "curl";
                p.StartInfo.Arguments = escapedArgs;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();

                while (!p.StandardOutput.EndOfStream)
                {
                    this.Result += p.StandardOutput.ReadToEnd();
                }

                this.Report(HandlerType.Curl.ToString(), escapedArgs, this.Result);
                
                Console.WriteLine(this.Result);
            }
            catch(Exception exc)
            {
                _log.Debug(exc);
            }
        }
    }
}