// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using ghosts.client.linux.handlers;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Models;
using System.Reflection;

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
        private Thread MonitorThread { get; set; }
        private FileSystemWatcher _timelineWatcher;
        private FileSystemWatcher _stopfileWatcher;  //watches for changes to config/stop.txt indicating a stop request

        public void Run()
        {
            try
            {
                var timeline = TimelineBuilder.GetTimeline();

                var dirName = TimelineBuilder.TimelineFilePath().DirectoryName;
                // now watch that file for changes
                if (_timelineWatcher == null && dirName != null) //you can change this to a bool if you want but checks if the object has been created
                {
                    _log.Trace("Timeline watcher starting and is null...");
                    _timelineWatcher = new FileSystemWatcher(dirName)
                    {
                        Filter = Path.GetFileName(TimelineBuilder.TimelineFilePath().Name)
                    };
                    _log.Trace($"watching {Path.GetFileName(TimelineBuilder.TimelineFilePath().Name)}");
                    _timelineWatcher.NotifyFilter = NotifyFilters.LastWrite;
                    _timelineWatcher.EnableRaisingEvents = true;
                    _timelineWatcher.Changed += OnChanged;
                }

                if (_stopfileWatcher == null && dirName != null)
                {

                    _log.Trace("Stopfile watcher is starting");
                    _stopfileWatcher = new FileSystemWatcher(dirName);
                    var stopFile = "stop.txt";
                    _stopfileWatcher.Filter = stopFile;
                    _stopfileWatcher.EnableRaisingEvents = true;
                    _stopfileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Attributes;
                    _stopfileWatcher.Changed += StopFileChanged;
                }

                //load into an managing object
                //which passes the timeline commands to handlers
                //and creates a thread to execute instructions over that timeline
                if (timeline.Status == Timeline.TimelineStatus.Run)
                {
                    RunEx(timeline);
                }
                else
                {
                    if (MonitorThread == null) return;
                    MonitorThread.Interrupt();
                    MonitorThread = null;
                }
            }
            catch (Exception exc)
            {
                _log.Error(exc);
            }
        }

        public static void StopTimeline(Guid timelineId)
        {
            foreach (var threadJob in Program.ThreadJobs.Where(x => x.TimelineId == timelineId))
            {
                try
                {
                    threadJob.Thread.Interrupt();
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }

                try
                {
                    threadJob.Thread.Join();
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }
            }
        }

        public static void Stop()
        {
            foreach (var threadJob in Program.ThreadJobs)
            {
                try
                {
                    threadJob.Thread.Interrupt();
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }

                try
                {
                    threadJob.Thread.Interrupt();
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }
            }
        }



        private static void StopCommon()
        {
            try
            {
                _log.Trace("Stopping all threads.");
                Stop();
                _log.Trace("All threads have been stopped.");
            }
            catch (Exception exception)
            {
                _log.Info(exception);
            }

        }

        private void StopFileChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                _log.Trace($"Stop file Watcher event raised: {e.FullPath} {e.Name} {e.ChangeType}");
                _log.Trace("Terminating existing tasks and exiting orchestrator");
                StopCommon();
                Thread.Sleep(5000);
                LogManager.Shutdown();  //shutdown all logging
                System.Environment.Exit(0); //exit
            }
            catch (Exception exc)
            {
                _log.Info(exc);
            }
        }


        private static void RunEx(Timeline timeline)
        {
            foreach (var handler in timeline.TimeLineHandlers)
            {
                ThreadLaunch(timeline, handler);
            }
        }

        public static void RunCommand(Timeline timeline, TimelineHandler handler)
        {
            ThreadLaunch(timeline, handler);
        }

        private static void ThreadLaunch(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                _log.Trace($"Attempting new thread for: {handler.HandlerType}");

                Thread t = null;
                // ReSharper disable once NotAccessedVariable
                object o;
                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (handler.HandlerType)
                {
                    case HandlerType.NpcSystem:
                        // ReSharper disable once RedundantAssignment
                        o = new NpcSystem(timeline, handler);
                        break;
                    case HandlerType.Command:
                        t = new Thread(() =>
                        {
                            o = new Bash(handler);
                        });
                        break;
                    case HandlerType.Curl:
                        t = new Thread(() =>
                        {
                            o = new Curl(handler);
                        });
                        break;
                    case HandlerType.BrowserChrome:
                        t = new Thread(() =>
                        {
                            o = new BrowserChrome(handler);
                        });
                        break;
                    case HandlerType.BrowserFirefox:
                        t = new Thread(() =>
                        {
                            o = new BrowserFirefox(handler);
                        });
                        break;
                    case HandlerType.Ssh:
                        t = new Thread(() =>
                        {
                            _ = new Ssh(handler);
                        });
                        break;
                    case HandlerType.Sftp:
                        t = new Thread(() =>
                        {
                            _ = new Sftp(handler);
                        });
                        break;
                    case HandlerType.Watcher:
                        t = new Thread(() =>
                        {
                            o = new Watcher(handler);
                        });
                        break;
                    case HandlerType.Aws:
                        t = new Thread(() =>
                        {
                            o = new Aws(handler);
                        });
                        break;
                    case HandlerType.Azure:
                        t = new Thread(() =>
                        {
                            o = new Azure(handler);
                        });
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                if (t == null)
                {
                    _log.Debug($"HandlerType {handler.HandlerType} not supported on this platform");
                    return;
                }

                t.IsBackground = true;
                t.Start();
                Program.ThreadJobs.Add(new ThreadJob
                {
                    TimelineId = timeline.Id,
                    Thread = t
                });
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                _log.Trace($"FileWatcher event raised: {e.FullPath} {e.Name} {e.ChangeType}");

                // filewatcher throws two events, we only need 1
                var lastWriteTime = File.GetLastWriteTime(e.FullPath);
                if (lastWriteTime == _lastRead) return;

                _lastRead = lastWriteTime;
                _log.Trace("FileWatcher Processing: " + e.FullPath + " " + e.ChangeType);
                _log.Trace($"Reloading {MethodBase.GetCurrentMethod()?.DeclaringType}");

                _log.Trace("terminate existing tasks and rerun orchestrator");

                StopCommon();
                Thread.Sleep(7500);

                try
                {
                    Run();
                }
                catch (Exception exception)
                {
                    _log.Info(exception);
                }
            }
            catch (Exception exc)
            {
                _log.Info(exc);
            }
        }
    }
}
