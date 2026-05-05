// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Microsoft.Win32;
using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Handlers;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain.Code;
using Ghosts.Domain.Models;

namespace Ghosts.Client.Universal.TimelineManager;

/// <summary>
/// Translates timeline.config file events into their appropriate handler
/// </summary>
public class Orchestrator
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static DateTime _lastRead = DateTime.MinValue;
    private static Timeline _defaultTimeline;
    private static FileSystemWatcher _timelineWatcher { get; set; }
    private static FileSystemWatcher _stopfileWatcher { get; set; } //watches for changes to config/stop.txt indicating a stop request
    private bool _isSafetyNetRunning;
    //private bool _isTempCleanerRunning;

    private bool IsWordInstalled { get; set; }
    private bool IsExcelInstalled { get; set; }
    private bool IsPowerPointInstalled { get; set; }
    private bool IsOutlookInstalled { get; set; }

    //[PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    public void Run()
    {
        try
        {
            _defaultTimeline = TimelineBuilder.GetTimeline();

            if (_isSafetyNetRunning != true) //checking if safetynet has already been started
            {
                this.StartSafetyNet(); //watch instance numbers
                _isSafetyNetRunning = true;
            }

            // if (_isTempCleanerRunning != true) //checking if tempcleaner has been started
            // {
            //     //TempFiles.StartTempFileWatcher(); //watch temp directory on a diff schedule
            //     _isTempCleanerRunning = true;
            // }


            var dirName = TimelineBuilder.TimelineFilePath().DirectoryName;
            _log.Trace($"In Orchestrator:Run, Timeline directory name: {dirName}");
            // now watch that file for changes
            if (_timelineWatcher == null &&
                dirName != null) //you can change this to a bool if you want but checks if the object has been created
            {
                _timelineWatcher = new FileSystemWatcher(dirName)
                {
                    Filter = Path.GetFileName(TimelineBuilder.TimelineFilePath().Name)
                };
                _log.Trace("Timeline watcher starting and is null...");
                _log.Trace($"watching {Path.GetFileName(TimelineBuilder.TimelineFilePath().Name)}");
                _timelineWatcher.NotifyFilter = NotifyFilters.LastWrite;
                _timelineWatcher.EnableRaisingEvents = true;
                _timelineWatcher.Changed += OnChanged;
            }

            if (_stopfileWatcher == null && dirName != null)
            {
                _log.Trace("Stopfile watcher is starting");
                _stopfileWatcher = new FileSystemWatcher(dirName);
                _stopfileWatcher.Filter = "stop.txt";
                _stopfileWatcher.EnableRaisingEvents = true;
                _stopfileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Attributes;
                _stopfileWatcher.Changed += StopFileChanged;
            }

            //load into an managing object
            //which passes the timeline commands to handlers
            //and creates a thread to execute instructions over that timeline
            if (_defaultTimeline.Status == Timeline.TimelineStatus.Run)
            {
                RunEx(_defaultTimeline);
            }
        }
        catch (Exception e)
        {
            _log.Error($"Orchestrator exception: {e}");
        }
    }

    public void StopTimeline(Guid timelineId)
    {
        foreach (var job in Program.RunningTasks.Values.Where(x => x.TimelineId == timelineId))
        {
            try
            {
                job.CancellationTokenSource.Cancel();
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }

            try
            {
                job.Thread.Join();
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
        }
    }

    public void Stop()
    {
        foreach (var job in Program.RunningTasks.Values)
        {
            try
            {
                if (job.CancellationTokenSource.Token.CanBeCanceled)
                {
                    job.CancellationTokenSource.Cancel();
                    _log.Trace($"Sent cancellation to job {job} ");
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }

            try
            {
                if (job.Thread != null && job.Thread.IsAlive)
                {
                    job.Thread.Join();
                    _log.Trace($"Thread job {job} has aborted.");
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
        }
    }

    private void RunEx(Timeline timeline)
    {
        WhatsInstalled();
        _log.Trace($"Entering Orchestrator: RunEx ");
        foreach (var handler in timeline.TimeLineHandlers)
        {
             _log.Trace($"In Orchestrator: RunEx, launching {handler.HandlerType.ToString()} ");
            ThreadLaunch(timeline, handler);
        }
        _log.Trace($"Exiting Orchestrator: RunEx ");
    }

    public void RunCommand(Timeline timeline, TimelineHandler handler)
    {
        WhatsInstalled();
         _log.Trace($"Entering Orchestrator: RunCommand ");
        ThreadLaunch(timeline, handler);
        _log.Trace($"Exiting Orchestrator: RunCommand ");
    }

    public void RunCommandCron(Timeline timeline, TimelineHandler handler)
    {
        WhatsInstalled();
        _ = ThreadLaunchEx(timeline, handler);
    }

    public static Task RunHandler(HandlerType type, Timeline timeline, TimelineHandler handler, CancellationToken token)
    {
        var typeName = $"Ghosts.Client.Universal.Handlers.{type}";
        var handlerType = Type.GetType(typeName);

        if (handlerType == null)
        {
            _log.Error($"Handler type '{type}' could not be resolved (looked for {typeName})");
            throw new NotSupportedException($"Handler type '{type}' not found.");
        }

        if (!typeof(IHandler).IsAssignableFrom(handlerType))
        {
            _log.Error($"{handlerType.Name} does not implement IHandler — cannot execute");
            throw new InvalidOperationException($"{handlerType.Name} does not implement IHandler");
        }

        var instance = (IHandler)Activator.CreateInstance(handlerType, timeline, handler, token);
        TaskCreationOptions options = (handler.Loop) ? TaskCreationOptions.LongRunning :  TaskCreationOptions.None;
        TaskFactory factory = new TaskFactory(options, TaskContinuationOptions.None);
        return factory.StartNew(() => instance?.Run(), token);

    }

    ///here lies technical debt
    //TODO clean up
    private void StartSafetyNet()
    {
        try
        {
            var t = new Thread(SafetyNet) { IsBackground = true, Name = "ghosts-safetynet" };
            t.Start(_defaultTimeline);
        }
        catch (Exception e)
        {
            _log.Error($"SafetyNet thread launch exception: {e}");
        }
    }

    ///here lies technical debt
    //TODO clean up
    // if supposed to be one excel running, and there is more than 2, then kill race condition
    private static void SafetyNet(object defaultTimeline)
    {
        var timeline = (Timeline)defaultTimeline;
        while (true)
        {
            try
            {
                _log.Trace("SafetyNet loop beginning");

                foreach (var handler in timeline.TimeLineHandlers)
                {
                    string processName = null;
                    switch (handler.HandlerType)
                    {
                        case HandlerType.BrowserChrome:
                            processName = "chrome";
                            break;
                        case HandlerType.BrowserFirefox:
                            processName = "firefox";
                            break;
                        case HandlerType.Word:
                            if (OperatingSystem.IsWindows()) processName = "WINWORD";
                            break;
                        case HandlerType.Excel:
                            if (OperatingSystem.IsWindows()) processName = "EXCEL";
                            break;
                        case HandlerType.PowerPoint:
                            if (OperatingSystem.IsWindows()) processName = "POWERPNT";
                            break;
                    }

                    if (processName == null) continue;

                    var handlerCount = timeline.TimeLineHandlers.Count(o => o.HandlerType == handler.HandlerType);
                    var processCount = Process.GetProcessesByName(processName).Length;
                    if (processCount > handlerCount + 1)
                    {
                        _log.Trace($"SafetyNet {handler.HandlerType} handlers: {handlerCount} processes: {processCount} - killing");
                        ProcessManager.KillProcessAndChildrenByName(processName);
                    }
                }

                _log.Trace("SafetyNet loop ending");
            }
            catch (Exception e)
            {
                _log.Trace($"SafetyNet exception: {e}");
            }
            finally
            {
                Thread.Sleep(300000); //clean up every 5 minutes
            }
        }
    }

    private void WhatsInstalled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using (var regWord = Registry.ClassesRoot.OpenSubKey("Outlook.Application"))
        {
            if (regWord != null)
            {
                IsOutlookInstalled = true;
            }

            _log.Trace($"Outlook is installed: {IsOutlookInstalled}");
        }

        using (var regWord = Registry.ClassesRoot.OpenSubKey("Word.Application"))
        {
            if (regWord != null)
            {
                IsWordInstalled = true;
            }

            _log.Trace($"Word is installed: {IsWordInstalled}");
        }

        using (var regWord = Registry.ClassesRoot.OpenSubKey("Excel.Application"))
        {
            if (regWord != null)
            {
                IsExcelInstalled = true;
            }

            _log.Trace($"Excel is installed: {IsExcelInstalled}");
        }

        using (var regWord = Registry.ClassesRoot.OpenSubKey("PowerPoint.Application"))
        {
            if (regWord != null)
            {
                IsPowerPointInstalled = true;
            }

            _log.Trace($"PowerPoint is installed: {IsPowerPointInstalled}");
        }
    }

    private void ThreadLaunch(Timeline timeline, TimelineHandler handler)
    {
        if (handler.ScheduleType == TimelineHandler.TimelineScheduleType.Cron)
        {
            _log.Trace($"Attempting new cron job for: {handler.HandlerType}");
            var s = new CronScheduling();
            Program.Scheduler.ScheduleJob(s.GetJob(handler), s.GetTrigger(handler));
            return;
        }

        _ = ThreadLaunchEx(timeline, handler);
    }

    private Task ThreadLaunchEx(Timeline timeline, TimelineHandler handler)
    {
        var cts = new CancellationTokenSource();

        try
        {
            _log.Info($"Starting handler {handler.HandlerType} for timeline {timeline.Id}");
            var task = RunHandler(handler.HandlerType, timeline, handler, cts.Token);

            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _log.Error($"Handler {handler.HandlerType} for timeline {timeline.Id} faulted: {t.Exception?.GetBaseException().Message}");
                    _log.Debug(t.Exception?.GetBaseException());
                }
                else if (t.IsCanceled)
                {
                    _log.Info($"Handler {handler.HandlerType} for timeline {timeline.Id} was cancelled");
                }
                else
                {
                    _log.Info($"Handler {handler.HandlerType} for timeline {timeline.Id} completed");
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            Program.RunningTasks.TryAdd(Guid.NewGuid(), new TaskJob { TimelineId = timeline.Id, CancellationTokenSource = cts, Task = task});
        }
        catch (Exception e)
        {
            _log.Error($"Failed to launch handler {handler.HandlerType} for timeline {timeline.Id}: {e.Message}");
            _log.Debug(e);
        }

        return Task.CompletedTask;
    }

    private void StopCommon()
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

        try
        {
            _log.Trace("Cleaning all processes.");
            StartupTasks.CleanupProcesses();
            _log.Trace("All processes have been cleaned.");
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
            Program.Scheduler?.Shutdown().GetAwaiter().GetResult();
            _log.Trace("Quartz terminated");
            LogManager.Shutdown(); //shutdown all logging
            Thread.Sleep(10000);
            Environment.Exit(0); //exit
        }
        catch (Exception exc)
        {
            _log.Info(exc);
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
