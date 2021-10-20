// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Linq;
using System.Threading;
using ghosts.client.linux.handlers;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Models;
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

        public void Run()
        {
            try
            {
                var timeline = TimelineBuilder.GetLocalTimeline();
            
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

        public void StopTimeline(Guid timelineId)
        {
            foreach (var threadJob in Program.ThreadJobs.Where(x=>x.TimelineId == timelineId))
            {
                try
                {
                    threadJob.Thread.Abort(null);
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

        public void Stop()
        {
            foreach (var threadJob in Program.ThreadJobs)
            {
                try
                {
                    threadJob.Thread.Abort(null);
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

        private void RunEx(Timeline timeline)
        {
            foreach (var handler in timeline.TimeLineHandlers)
            {
                ThreadLaunch(timeline, handler);
            }
        }

        public void RunCommand(Timeline timeline, TimelineHandler handler)
        {
            ThreadLaunch(timeline, handler);
        }

        private void ThreadLaunch(Timeline timeline, TimelineHandler handler)
        {
            try
            {
                _log.Trace($"Attempting new thread for: {handler.HandlerType}");

                Thread t = null;
                object o;
                switch (handler.HandlerType)
                {
                    case HandlerType.NpcSystem:
                        var npc = new NpcSystem(timeline, handler);
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
    }
 }
