// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ghosts.client.linux.handlers;
using Ghosts.Domain;
using Ghosts.Domain.Code;
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
                timelineWatcher.Changed += OnChanged;

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
                ThreadLaunch(timeline, handler);
            }
        }

        public void RunCommand(Timeline timeline, TimelineHandler handler)
        {
            this.WhatsInstalled();
            ThreadLaunch(timeline, handler);
        }

        private void WhatsInstalled()
        {
            //TODO: check that used applications exist
        }

        private void ThreadLaunch(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                _log.Trace($"Attempting new thread for: {handler.HandlerType}");

                Thread t = null;
                var threadJob = new ThreadJob
                {
                    Id = Guid.NewGuid().ToString(),
                    Handler = handler,
                    TimelineId = timeline.Id
                };

                object o;
                switch (handler.HandlerType)
                {
                    case HandlerType.NpcSystem:
                        //var npc = new NpcSystem(handler);
                        //break;
                    case HandlerType.Command:
                        t = new Thread(() =>
                        {
                            o = new Bash(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();
                        break;
                    case HandlerType.Curl:
                        t = new Thread(() =>
                        {
                            o = new Curl(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();
                        break;
                    case HandlerType.BrowserFirefox:
                        t = new Thread(() =>
                        {
                            o = new BrowserFirefox(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();
                        break;
                }

                if (t != null)
                {
                    this._threads.Add(t);
                }
                
                if (threadJob.ProcessName != null)
                {
                    this._threadJobs.Add(threadJob);
                }
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // filewatcher throws two events, we only need 1
            var lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime <= _lastRead.AddSeconds(1)) return;
            
            _lastRead = lastWriteTime;
            _log.Trace("File: " + e.FullPath + " " + e.ChangeType);
            
            var method = string.Empty;
            if (System.Reflection.MethodBase.GetCurrentMethod() != null)
            {
                var declaringType = System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType;
                if (declaringType != null)
                    method = declaringType?.ToString();
            }
            _log.Trace($"Reloading {method}...");

            // now terminate existing tasks and rerun
            this.Shutdown();
            //StartupTasks.CleanupProcesses();
            this.Run();
        }
    }

    public class ThreadJob
    {
        public string Id { get; set; }
        public Guid TimelineId { get; set; }
        public TimelineHandler Handler { get; set; }
        public string ProcessName { get; set; }
    }
}
