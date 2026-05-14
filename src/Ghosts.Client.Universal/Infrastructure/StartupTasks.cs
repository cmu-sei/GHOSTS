// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure
{
    /// <summary>
    /// Some apps (word, excel, etc.) like to hang around and leech memory on client machines
    /// this class attempts to kill those pesky applications on GHOSTS
    /// startup and shutdown (and maybe periodically in-between)
    /// </summary>
    public static class StartupTasks
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void CleanupProcesses()
        {
            try
            {
                var cleanupList = new List<string>();

                var timeline = TimelineBuilder.GetTimeline();
                if (timeline?.TimeLineHandlers != null)
                {
                    foreach (var handler in timeline.TimeLineHandlers)
                    {
                        switch (handler.HandlerType)
                        {
                            case HandlerType.BrowserChrome:
                                cleanupList.Add(ProcessManager.ProcessNames.Chrome);
                                cleanupList.Add(ProcessManager.ProcessNames.ChromeDriver);
                                break;
                            case HandlerType.BrowserFirefox:
                                cleanupList.Add(ProcessManager.ProcessNames.Firefox);
                                cleanupList.Add(ProcessManager.ProcessNames.GeckoDriver);
                                break;
                            case HandlerType.Word:
                                cleanupList.Add(ProcessManager.ProcessNames.Word);
                                break;
                            case HandlerType.Excel:
                                cleanupList.Add(ProcessManager.ProcessNames.Excel);
                                break;
                            case HandlerType.PowerPoint:
                                cleanupList.Add(ProcessManager.ProcessNames.PowerPoint);
                                break;
                            case HandlerType.Outlook:
                            case HandlerType.Outlookv2:
                                cleanupList.Add(ProcessManager.ProcessNames.Outlook);
                                break;
                            case HandlerType.Command:
                                cleanupList.Add(ProcessManager.ProcessNames.Command);
                                break;
                            case HandlerType.PowerShell:
                                cleanupList.Add(ProcessManager.ProcessNames.PowerShell);
                                break;
                            case HandlerType.Curl:
                                cleanupList.Add(ProcessManager.ProcessNames.Curl);
                                break;
                        }
                    }
                }

                if (!Program.Configuration.AllowMultipleInstances)
                {
                    var ghosts = Process.GetCurrentProcess();
                    cleanupList.Add(ghosts.ProcessName);
                    _log.Trace($"Got ghosts pid: {ghosts.Id}");
                }

                foreach (var cleanupItem in cleanupList)
                {
                    try
                    {
                        new Thread(() =>
                        {
                            Thread.CurrentThread.IsBackground = true;
                            ProcessManager.KillProcessAndChildrenByName(cleanupItem);
                        }).Start();
                        _log.Trace($"Killing {cleanupItem}");
                    }
                    catch
                    {
                        _log.Debug($"Proving hard to kill - Cleanup failed on process: {cleanupItem}");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Debug($"Cleanup process exception: {e}");
            }
        }

        /// <summary>
        /// make sure ghosts starts when machine starts
        /// </summary>
        public static void SetStartup()
        {
            // ignored
        }
    }
}
