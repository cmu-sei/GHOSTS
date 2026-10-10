// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
                        cleanupList.AddRange(ProcessManager.GetProcessNames(handler.HandlerType));
                    }
                }

                if (!Program.Configuration.AllowMultipleInstances)
                {
                    // Other instances run as "Ghosts.Client.Universal", or as "dotnet" when started with "dotnet Ghosts.Client.Universal.dll"
                    cleanupList.Add(ApplicationDetails.Name);
                    foreach (var pid in ProcessManager.GetDotnetHostedPids(ApplicationDetails.Name))
                    {
                        ProcessManager.KillProcessAndChildrenByPid(pid);
                    }
                    _log.Trace($"Got ghosts pid: {Process.GetCurrentProcess().Id}");
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
