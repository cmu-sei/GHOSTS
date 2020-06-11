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

                Ex(handler);
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

                        this.Command(handler.Initial, timelineEvent.Command);

                        foreach (var cmd in timelineEvent.CommandArgs)
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                                this.Command(handler.Initial, cmd.ToString());
                        break;
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(timelineEvent.DelayAfter);
            }
        }

        private void Command(string initial, string command)
        {
            var escapedArgs = command.Replace("\"", "\\\"");

            var p = new Process();
            //p.EnableRaisingEvents = false;
            p.StartInfo.FileName = string.IsNullOrEmpty(initial) ? "bash" : initial;
            p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            //* Set your output and error (asynchronous) handlers
            p.OutputDataReceived += OutputHandler;
            p.ErrorDataReceived += ErrorHandler;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            while (!p.StandardOutput.EndOfStream)
            {
                this.Result += p.StandardOutput.ReadToEnd();
            }

            p.WaitForExit();

            this.Report(HandlerType.Command.ToString(), escapedArgs, this.Result);
        }

        void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            this.Result += outLine.Data;
        }

        void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            Console.WriteLine(outLine.Data);
        }
    }
}