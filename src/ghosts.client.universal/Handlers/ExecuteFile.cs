// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using ReportItem = Ghosts.Domain.Code.ReportItem;

namespace Ghosts.Client.Universal.Handlers
{
    public class ExecuteFile : BaseHandler
    {
        public int executionprobability = 100;
        public int jitterfactor { get; set; } = 0;  //used with Jitter.JitterFactorDelay
        public string outFolder = "C:\\users\\public";
        public string folderpath = "C:\\temp";
        public int execWaitTime = 1000;
        public ExecuteFile(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
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
            catch (ThreadAbortException)
            {
                //ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Command);
                _log.Trace($"Execute closing...");
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {

            if (handler.HandlerArgs.ContainsKey("execution-probability"))
            {
                int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out executionprobability);
                if (executionprobability < 0 || executionprobability > 100) executionprobability = 100;
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }
            if (handler.HandlerArgs.ContainsKey("execWaitTime"))
            {
                int.TryParse(handler.HandlerArgs["execWaitTime"].ToString(),out execWaitTime);
            }
            if (handler.HandlerArgs.ContainsKey("outFolder"))
            {
                outFolder = handler.HandlerArgs["outFolder"].ToString();
                if (outFolder.Contains("%")) { outFolder = Environment.ExpandEnvironmentVariables(outFolder); }
            }
            if (handler.HandlerArgs.ContainsKey("folderPath"))
            {
                folderpath = handler.HandlerArgs["folderPath"].ToString();
                if (folderpath.Contains("%")) { folderpath = Environment.ExpandEnvironmentVariables(folderpath); }
            }
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                switch (timelineEvent.Command)
                {
                    case "random":
                        while (true)
                        {
                            if (executionprobability < _random.Next(0, 100))
                            {
                                //skipping this command
                                _log.Trace($"Execuation skipped due to execution probability");
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                                continue;
                            }
                            this.Command(handler, timelineEvent);

                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                        }
                    default:
                        this.Command(handler, timelineEvent);
                        break;
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(timelineEvent.DelayAfterActual);
            }
        }

        public void Command(TimelineHandler handler, TimelineEvent timelineEvent)
        {
            string[] fileArray = Directory.GetFiles($@"{folderpath}", "*", SearchOption.AllDirectories);
            if (fileArray.Length > 0)
            {
                foreach (var fname in fileArray)
                {
                    FileInfo info = new FileInfo(fname);
                    _log.Trace($"Executing {info.Name}");
                    var process = Process.Start(info.FullName);
                    _log.Trace($"Waiting {execWaitTime} before stopping process {info.Name}");
                    Thread.Sleep(execWaitTime);
                    if (process != null)
                    {
                        _log.Trace($"Killing process: {info.Name}");
                        process.Kill();
                        Thread.Sleep(10000);
                        process.Close();
                        //this.Report(handler.HandlerType.ToString(), info.FullName, timelineEvent.TrackableId);
                        Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = info.FullName, Trackable = timelineEvent.TrackableId});
                    }
                    else
                        _log.Trace($"Failed to start {info.Name}");
                    if (!string.IsNullOrEmpty(outFolder))
                    {
                        if (!Directory.Exists(outFolder))
                            Directory.CreateDirectory(outFolder);
                        if (Directory.Exists(outFolder))
                        {
                            string newFullPath = Path.Combine(outFolder, info.Name);
                            string filename = Path.GetFileNameWithoutExtension(info.FullName);
                            string extension = Path.GetExtension(info.FullName);
                            int count = 1;

                            while (File.Exists(newFullPath))
                            {
                                string tempFileName = string.Format("{0} ({1})", filename, count++);
                                newFullPath = Path.Combine(outFolder, tempFileName + extension);
                            }
                            _log.Trace($"Moving {info.Name} to {outFolder}");
                            info.MoveTo(newFullPath);
                        }
                    }
                }

            }

        }
    }
}
