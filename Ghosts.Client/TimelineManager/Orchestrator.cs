// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Client.Code;
using Ghosts.Client.Handlers;
using Ghosts.Domain;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Ghosts.Client.TimelineManager
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

        private bool _isWordInstalled { get; set; }
        private bool _isExcelInstalled { get; set; }
        private bool _isPowerPointInstalled { get; set; }
        private bool _isOutlookInstalled { get; set; }

        public void Run()
        {
            Timeline timeline = TimelineBuilder.GetLocalTimeline();

            // now watch that file for changes
            FileSystemWatcher timelineWatcher = new FileSystemWatcher(TimelineBuilder.TimelineFilePath().DirectoryName)
            {
                Filter = Path.GetFileName(TimelineBuilder.TimelineFilePath().Name)
            };
            _log.Trace($"watching {timelineWatcher.Path}");
            timelineWatcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite;
            timelineWatcher.EnableRaisingEvents = true;
            timelineWatcher.Changed += new FileSystemEventHandler(OnChanged);

            _threadJobs = new List<ThreadJob>();

            //load into an managing object
            //which passes the timeline commands to handlers
            //and creates a thread to execute instructions over that timeline
            if (timeline.Status == Timeline.TimelineStatus.Run)
            {
                RunEx(timeline);
            }
            else
            {
                if (MonitorThread != null)
                {
                    MonitorThread.Abort();
                    MonitorThread = null;
                }
            }
        }

        public void Shutdown()
        {
            foreach (Thread thread in _threads)
            {
                thread.Abort(null);
            }
        }

        private void RunEx(Timeline timeline)
        {
            _threads = new List<Thread>();

            WhatsInstalled();

            foreach (TimelineHandler handler in timeline.TimeLineHandlers)
            {
                ThreadLaunch(handler);
            }

            MonitorThread = new Thread(ThreadMonitor)
            {
                IsBackground = true
            };
            MonitorThread.Start();
        }

        public void RunCommand(TimelineHandler handler)
        {
            WhatsInstalled();
            ThreadLaunch(handler);
        }

        private void WhatsInstalled()
        {
            using (RegistryKey regWord = Registry.ClassesRoot.OpenSubKey("Outlook.Application"))
            {
                if (regWord != null)
                {
                    _isOutlookInstalled = true;
                }

                _log.Trace($"Outlook is installed: {_isOutlookInstalled}");
            }

            using (RegistryKey regWord = Registry.ClassesRoot.OpenSubKey("Word.Application"))
            {
                if (regWord != null)
                {
                    _isWordInstalled = true;
                }

                _log.Trace($"Word is installed: {_isWordInstalled}");
            }

            using (RegistryKey regWord = Registry.ClassesRoot.OpenSubKey("Excel.Application"))
            {
                if (regWord != null)
                {
                    _isExcelInstalled = true;
                }

                _log.Trace($"Excel is installed: {_isExcelInstalled}");
            }

            using (RegistryKey regWord = Registry.ClassesRoot.OpenSubKey("PowerPoint.Application"))
            {
                if (regWord != null)
                {
                    _isPowerPointInstalled = true;
                }

                _log.Trace($"PowerPoint is installed: {_isPowerPointInstalled}");
            }
        }

        private void ThreadLaunch(TimelineHandler handler)
        {
            try
            {
                _log.Trace($"Attempting new thread for: {handler.HandlerType}");

                Thread t = null;
                ThreadJob threadJob = new ThreadJob
                {
                    Id = Guid.NewGuid().ToString(),
                    Handler = handler
                };

                switch (handler.HandlerType)
                {
                    case HandlerType.NpcSystem:
                        NpcSystem npc = new NpcSystem(handler);
                        break;
                    case HandlerType.Command:
                        t = new Thread(() =>
                        {
                            Cmd o = new Cmd(handler);

                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();

                        threadJob.ProcessName = ProcessManager.ProcessNames.Command;

                        break;
                    case HandlerType.Word:
                        _log.Trace("Launching thread for word");
                        if (_isWordInstalled)
                        {
                            t = new Thread(() =>
                            {
                                WordHandler o = new WordHandler(handler);
                            })
                            {
                                IsBackground = true,
                                Name = threadJob.Id
                            };
                            t.Start();

                            threadJob.ProcessName = ProcessManager.ProcessNames.Word;
                        }
                        break;
                    case HandlerType.Excel:
                        _log.Trace("Launching thread for excel");
                        if (_isExcelInstalled)
                        {
                            t = new Thread(() =>
                            {
                                ExcelHandler o = new ExcelHandler(handler);
                            })
                            {
                                IsBackground = true,
                                Name = threadJob.Id
                            };
                            t.Start();

                            threadJob.ProcessName = ProcessManager.ProcessNames.Excel;
                        }
                        break;
                    case HandlerType.Clicks:
                        _log.Trace("Launching thread to handle clicks");
                        t = new Thread(() =>
                        {
                            Clicks o = new Clicks(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();
                        break;
                    case HandlerType.Reboot:
                        _log.Trace("Launching thread to handle reboot");
                        t = new Thread(() =>
                        {
                            Reboot o = new Reboot(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();
                        break;
                    case HandlerType.PowerPoint:
                        _log.Trace("Launching thread for powerpoint");
                        if (_isPowerPointInstalled)
                        {
                            t = new Thread(() =>
                            {
                                PowerPointHandler o = new PowerPointHandler(handler);
                            })
                            {
                                IsBackground = true,
                                Name = threadJob.Id
                            };
                            t.Start();

                            threadJob.ProcessName = ProcessManager.ProcessNames.PowerPoint;
                        }
                        break;
                    case HandlerType.Outlook:
                        _log.Trace("Launching thread for outlook - note we're not checking if outlook installed, just going for it");
                        //if (this.IsOutlookInstalled)
                        //{
                        t = new Thread(() =>
                        {
                            Outlook o = new Outlook(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();

                        threadJob.ProcessName = ProcessManager.ProcessNames.Outlook;
                        //}

                        break;
                    case HandlerType.BrowserIE:
                        //IE demands COM apartmentstate be STA so diff thread creation required
                        t = new Thread(() =>
                        {
                            BrowserIE o = new BrowserIE(handler);
                        });
                        t.SetApartmentState(ApartmentState.STA);
                        t.IsBackground = true;
                        t.Name = threadJob.Id;
                        t.Start();

                        break;
                    case HandlerType.Notepad:
                        //TODO
                        t = new Thread(() =>
                        {
                            Notepad o = new Notepad(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();

                        break;

                    case HandlerType.BrowserChrome:
                        t = new Thread(() =>
                        {
                            BrowserChrome o = new BrowserChrome(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();

                        threadJob.ProcessName = ProcessManager.ProcessNames.Chrome;

                        break;
                    case HandlerType.BrowserFirefox:
                        t = new Thread(() =>
                        {
                            BrowserFirefox o = new BrowserFirefox(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();

                        threadJob.ProcessName = ProcessManager.ProcessNames.Firefox;

                        break;
                    case HandlerType.Watcher:
                        t = new Thread(() =>
                        {
                            Watcher o = new Watcher(handler);
                        })
                        {
                            IsBackground = true,
                            Name = threadJob.Id
                        };
                        t.Start();

                        //threadJob.ProcessName = ProcessManager.ProcessNames.Watcher;

                        break;
                }

                if (threadJob.ProcessName != null)
                {
                    _threadJobs.Add(threadJob);
                }

                if (t != null)
                {
                    _threads.Add(t);
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

            //Add office docs override
            if (true == true)
            {
                if (_threadJobs.All(o => o.ProcessName != ProcessManager.ProcessNames.Word))
                {
                    _threadJobs.Add(new ThreadJob { ProcessName = ProcessManager.ProcessNames.Word, Handler = new TimelineHandler { HandlerType = HandlerType.Word, Loop = false } });
                }
                if (_threadJobs.All(o => o.ProcessName != ProcessManager.ProcessNames.Excel))
                {
                    _threadJobs.Add(new ThreadJob { ProcessName = ProcessManager.ProcessNames.Excel, Handler = new TimelineHandler { HandlerType = HandlerType.Excel, Loop = false } });
                }
                if (_threadJobs.All(o => o.ProcessName != ProcessManager.ProcessNames.PowerPoint))
                {
                    _threadJobs.Add(new ThreadJob { ProcessName = ProcessManager.ProcessNames.PowerPoint, Handler = new TimelineHandler { HandlerType = HandlerType.PowerPoint, Loop = false } });
                }
                if (_threadJobs.All(o => o.ProcessName != ProcessManager.ProcessNames.Outlook))
                {
                    _threadJobs.Add(new ThreadJob { ProcessName = ProcessManager.ProcessNames.Outlook, Handler = new TimelineHandler { HandlerType = HandlerType.Outlook, Loop = false } });
                }
            }

            ThreadJob[] jobs = _threadJobs.ToArray();
            while (true)
            {
                Thread.Sleep(30000);
                //first, get all jobs and if not running, run a new one
                foreach (ThreadJob job in jobs)
                {
                    int procCount = ProcessManager.GetPids(job.ProcessName).Count();
                    _log.Trace($"PID count for {job.ProcessName} is {procCount}");
                    if (procCount < 1)
                    {
                        if (job.Handler.Loop)
                        {
                            _log.Trace($"Redundant killing process and children: {job.ProcessName}");
                            ProcessManager.KillProcessAndChildrenByName(job.ProcessName);

                            //refire handler job
                            _log.Trace($"Threadlaunching job: {job.Handler.HandlerType}");
                            ThreadLaunch(job.Handler);
                        }
                    }

                    Thread.Sleep(30000);
                    //if multiple instances of something are running, kill all but one
                    List<int> pids = ProcessManager.GetPids(job.ProcessName).ToList();

                    int limit = 1;
                    if (job.Handler.HandlerType == HandlerType.BrowserChrome || job.Handler.HandlerType == HandlerType.BrowserFirefox)
                    {
                        limit = 7;
                    }

                    int pidCount = pids.Count();
                    _log.Trace($"PID count for {job.ProcessName} was {pidCount} (limit {limit}");
                    if (pidCount > limit)
                    {
                        for (int i = 0; i < pids.Count() - 1; i++)
                        {
                            _log.Trace($"Killing PID for {job.ProcessName}: {pids[i]}");
                            ProcessManager.KillProcessAndChildrenByPid(pids[i]);

                        }
                    }
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
                Shutdown();
                StartupTasks.CleanupProcesses();
                Run();
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
