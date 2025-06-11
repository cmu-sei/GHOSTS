// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using ReportItem = Ghosts.Domain.Code.ReportItem;

namespace Ghosts.Client.Universal.Handlers;

public class ExecuteFile(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    public int executionprobability = 100;
    public int jitterfactor { get; set; } = 0; //used with Jitter.JitterFactorDelay
    public string outFolder = "C:\\users\\public";
    public string folderpath = "C:\\temp";
    public int execWaitTime = 1000;

    protected override Task RunOnce()
    {
        if (this.Handler.HandlerArgs.ContainsKey("execution-probability"))
        {
            int.TryParse(this.Handler.HandlerArgs["execution-probability"].ToString(), out executionprobability);
            if (executionprobability < 0 || executionprobability > 100) executionprobability = 100;
        }

        if (this.Handler.HandlerArgs.ContainsKey("delay-jitter"))
        {
            jitterfactor = Jitter.JitterFactorParse(this.Handler.HandlerArgs["delay-jitter"].ToString());
        }

        if (this.Handler.HandlerArgs.ContainsKey("execWaitTime"))
        {
            int.TryParse(this.Handler.HandlerArgs["execWaitTime"].ToString(), out execWaitTime);
        }

        if (this.Handler.HandlerArgs.ContainsKey("outFolder"))
        {
            outFolder = this.Handler.HandlerArgs["outFolder"].ToString();
            if (outFolder.Contains("%"))
            {
                outFolder = Environment.ExpandEnvironmentVariables(outFolder);
            }
        }

        if (this.Handler.HandlerArgs.ContainsKey("folderPath"))
        {
            folderpath = this.Handler.HandlerArgs["folderPath"].ToString();
            if (folderpath.Contains("%"))
            {
                folderpath = Environment.ExpandEnvironmentVariables(folderpath);
            }
        }

        foreach (var timelineEvent in this.Handler.TimeLineEvents)
        {
            WorkingHours.Is(this.Handler);

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

                        ProcessCommand(this.Handler, timelineEvent);

                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                    }
                default:
                    ProcessCommand(this.Handler, timelineEvent);
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
                Thread.Sleep(timelineEvent.DelayAfterActual);
        }

        return Task.CompletedTask;
    }

    public void ProcessCommand(TimelineHandler handler, TimelineEvent timelineEvent)
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
                    Report(new ReportItem
                    {
                        Handler = handler.HandlerType.ToString(),
                        Command = info.FullName,
                        Trackable = timelineEvent.TrackableId
                    });
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
