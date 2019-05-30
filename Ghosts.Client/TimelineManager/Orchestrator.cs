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
using System.Reflection;
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
        private Timeline _timeline;

        private bool _isWordInstalled { get; set; }
        private bool _isExcelInstalled { get; set; }
        private bool _isPowerPointInstalled { get; set; }
        private bool _isOutlookInstalled { get; set; }

        public void Run()
        {
            try
            {
                this.StartSafetyNet(); //watch instance numbers
                TempFiles.StartTempFileWatcher(); //watch temp directory on a diff schedule

                this._timeline = TimelineBuilder.GetLocalTimeline();

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
                if (this._timeline.Status == Timeline.TimelineStatus.Run)
                {
                    RunEx(this._timeline);
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
            catch (Exception e)
            {
                _log.Error($"Orchestrator.Run exception: {e}");
            }
        }

        public void Shutdown()
        {
            try
            {
                foreach (var thread in _threads)
                {
                    thread.Abort(null);
                }
            }
            catch { }
        }

        private void RunEx(Timeline timeline)
        {
            _threads = new List<Thread>();

            WhatsInstalled();

            foreach (TimelineHandler handler in timeline.TimeLineHandlers)
            {
                ThreadLaunch(timeline, handler);
            }
        }

        public void RunCommand(TimelineHandler handler)
        {
            WhatsInstalled();
            ThreadLaunch(null, handler);
        }

        ///here lies technical debt
        //TODO clean up
        private void StartSafetyNet()
        {
            try
            {
                var t = new Thread(SafetyNet)
                {
                    IsBackground = true,
                    Name = "ghosts-safetynet"
                };
                t.Start();
            }
            catch (Exception e)
            {
                _log.Error($"SafetyNet thread launch exception: {e}");
            }
        }

        ///here lies technical debt
        //TODO clean up
        // if supposed to be one excel running, and there is more than 2, then kill race condition
        private static void SafetyNet()
        {
            while (true)
            {
                try
                {
                    _log.Trace("SafetyNet loop beginning");
                    var timeline = TimelineBuilder.GetLocalTimeline();

                    var handlerCount = timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel);
                    var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                    if (pids.Count > handlerCount + 1)
                    {
                        _log.Trace($"SafetyNet excel handlers: {handlerCount} pids: {pids.Count} - killing");
                        ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Excel);
                    }

                    handlerCount = timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.PowerPoint);
                    pids = ProcessManager.GetPids(ProcessManager.ProcessNames.PowerPoint).ToList();
                    if (pids.Count > handlerCount + 1)
                    {
                        _log.Trace($"SafetyNet powerpoint handlers: {handlerCount} pids: {pids.Count} - killing");
                        ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.PowerPoint);
                    }

                    handlerCount = timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Word);
                    pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Word).ToList();
                    if (pids.Count > handlerCount + 1)
                    {
                        _log.Trace($"SafetyNet word handlers: {handlerCount} pids: {pids.Count} - killing");
                        ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Word);
                    }
                    _log.Trace("SafetyNet loop ending");
                }
                catch (Exception e)
                {
                    _log.Trace($"SafetyNet exception: {e}");
                }
                finally
                {
                    Thread.Sleep(60000); //every 60 seconds clean up
                }
            }
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

        private void ThreadLaunch(Timeline timeline, TimelineHandler handler)
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
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Word).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Word))
                                return;

                            t = new Thread(() =>
                            {
                                WordHandler o = new WordHandler(timeline, handler);
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
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.Excel).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.Excel))
                                return;

                            t = new Thread(() =>
                            {
                                ExcelHandler o = new ExcelHandler(timeline, handler);
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
                            var pids = ProcessManager.GetPids(ProcessManager.ProcessNames.PowerPoint).ToList();
                            if (pids.Count > timeline.TimeLineHandlers.Count(o => o.HandlerType == HandlerType.PowerPoint))
                                return;

                            t = new Thread(() =>
                            {
                                PowerPointHandler o = new PowerPointHandler(timeline, handler);
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
        
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            try
            {
                _log.Trace($"FileWatcher event raised: {e.FullPath} {e.Name} {e.ChangeType}");

                // filewatcher throws two events, we only need 1
                DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
                if (lastWriteTime > _lastRead.AddSeconds(1))
                {
                    _lastRead = lastWriteTime;
                    _log.Trace("FileWatcher Processing: " + e.FullPath + " " + e.ChangeType);
                    _log.Trace($"Reloading {MethodBase.GetCurrentMethod().DeclaringType}");

                    _log.Trace("terminate existing tasks and rerun orchestrator");
                    
                    try
                    {
                        Shutdown();
                    }
                    catch (Exception exception)
                    {
                        _log.Info(exception);
                    }

                    try
                    {
                        StartupTasks.CleanupProcesses();
                    }
                    catch (Exception exception)
                    {
                        _log.Info(exception);
                    }

                    try
                    {
                        Run();
                    }
                    catch (Exception exception)
                    {
                        _log.Info(exception);
                    }
                }
            }
            catch (Exception exc)
            {
                _log.Info(exc);
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
