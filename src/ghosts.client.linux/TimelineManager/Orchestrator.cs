// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ghosts.client.linux.handlers;
using Ghosts.Domain;
using NLog;

namespace ghosts.client.linux.timelineManager
{
    /// <summary>
    /// Translates timeline.config file events into their appropriate handler
    /// </summary>
    public class Orchestrator
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static DateTime _lastRead = DateTime.MinValue;
        private List<Thread> _threads { get; set; }
        private List<ThreadJob> _threadJobs { get; set; }
        private Thread MonitorThread { get; set; }

        public void Run()
        {
            try
            {
                var timeline = TimelineBuilder.GetLocalTimeline();
            
                // now watch that file for changes
                var timelineWatcher = new FileSystemWatcher(TimelineBuilder.TimelineFilePath().DirectoryName);
                timelineWatcher.Filter = Path.GetFileName(TimelineBuilder.TimelineFilePath().Name);
                _log.Trace($"watching {timelineWatcher.Path}");
                timelineWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite;
                timelineWatcher.EnableRaisingEvents = true;
                timelineWatcher.Changed += new FileSystemEventHandler(OnChanged);

                this._threadJobs = new List<ThreadJob>();

                //load into an managing object
                //which passes the timeline commands to handlers
                //and creates a thread to execute instructions over that timeline
                if (timeline.Status == Timeline.TimelineStatus.Run)
                {
                    this.RunEx(timeline);
                }
                else
                {
                    if (this.MonitorThread != null)
                    {
                        this.MonitorThread.Abort();
                        this.MonitorThread = null;
                    }
                }
            }
            catch (Exception exc)
            {
                _log.Error(exc);
            }
        }

        public void Shutdown()
        {
            foreach (var thread in this._threads)
            {
                thread.Abort(null);
            }
        }

        private void RunEx(Timeline timeline)
        {
            this._threads = new List<Thread>();
            
            this.WhatsInstalled();

            foreach (var handler in timeline.TimeLineHandlers)
            {
                ThreadLaunch(handler);
            }

            this.MonitorThread = new Thread(this.ThreadMonitor);
            this.MonitorThread.IsBackground = true;
            this.MonitorThread.Start();
        }

        public void RunCommand(TimelineHandler handler)
        {
            this.WhatsInstalled();
            ThreadLaunch(handler);
        }

        private void WhatsInstalled()
        {
            //TODO: check that used applications exist
        }

        private void ThreadLaunch(TimelineHandler handler)
        {
            try
            {
                _log.Trace($"Attempting new thread for: {handler.HandlerType}");

                Thread t = null;
                var threadJob = new ThreadJob();
                threadJob.Id = Guid.NewGuid().ToString();
                threadJob.Handler = handler;

                switch (handler.HandlerType)
                {
                    case HandlerType.NpcSystem:
                        //var npc = new NpcSystem(handler);
                        //break;
                    case HandlerType.Command:
                        t = new Thread(() =>
                        {
                            var bash = new Bash(handler);

                        });
                        t.IsBackground = true;
                        t.Name = threadJob.Id;
                        t.Start();

                        //threadJob.ProcessName = ProcessManager.ProcessNames.Command;
                        break;
                    case HandlerType.Curl:
                        t = new Thread(() =>
                        {
                            var curl = new Curl(handler);

                        });
                        t.IsBackground = true;
                        t.Name = threadJob.Id;
                        t.Start();

                        //threadJob.ProcessName = ProcessManager.ProcessNames.Command;
                        break;
                }

                if (threadJob.ProcessName != null)
                {
                    this._threadJobs.Add(threadJob);
                }

                if (t != null)
                {
                    this._threads.Add(t);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void ThreadMonitor()
        {
            //this should be the original list only
            var jobs = this._threadJobs.ToArray();
            while (true)
            {
                Thread.Sleep(30000);
                //first, get all jobs and if not running, run a new one
                foreach (var job in jobs)
                {
                    //TODO
                }
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // filewatcher throws two events, we only need 1
            DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime > _lastRead.AddSeconds(1))
            {
                _lastRead = lastWriteTime;
                _log.Trace("File: " + e.FullPath + " " + e.ChangeType);
                _log.Trace($"Reloading {System.Reflection.MethodBase.GetCurrentMethod().DeclaringType}");

                // now terminate existing tasks and rerun
                this.Shutdown();
                //StartupTasks.CleanupProcesses();
                this.Run();
            }
        }
    }

    public class ThreadJob
    {
        public string Id { get; set; }
        public TimelineHandler Handler { get; set; }
        public string ProcessName { get; set; }
    }
}
