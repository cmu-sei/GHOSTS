// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NLog;

namespace ghosts.client.linux.Infrastructure
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
                var cleanupList = new List<string>
                {
                    //TODO: What processes are we managing?
                };

                //need to kill any other instance of ghosts already running
                var ghosts = Process.GetCurrentProcess();
                cleanupList.Add(ghosts.ProcessName);

                _log.Trace($"Got ghosts pid: {ghosts.Id}");

                foreach (var cleanupItem in cleanupList)
                {
                    try
                    {
                        new Thread(() =>
                        {
                            Thread.CurrentThread.IsBackground = true;
                            foreach (var process in Process.GetProcessesByName(cleanupItem))
                            {
                                if (process.Id != ghosts.Id) //don't kill thyself
                                {
                                    try
                                    {
                                        process.Kill();
                                    }
                                    catch { }
                                }
                            }
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
            return;
            /*
            try
            {
                throw new NotImplementedException();
                
                [Unit]
                Description=GHOSTS NPC Orchestration
                After=multi-user.target

                [Service]
                Type=simple
                ExecStart=/usr/bin/ghosts

                [Install]
                WantedBy=multi-user.target
              

                //_log.Trace("Startup set successfully");
            }
            catch (Exception e)
            {
                //_log.Debug($"Set startup: {e}");
            }
            */
        }
    }
}
