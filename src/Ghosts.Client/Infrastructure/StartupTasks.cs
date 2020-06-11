// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Ghosts.Client.Infrastructure.Email;
using NLog;
using Microsoft.Win32;

namespace Ghosts.Client.Infrastructure
{
    /// <summary>
    /// Some apps (word, excel, etc.) like to hang around and leech memory on client machines
    /// this class attempts to kill those pesky applications on GHOSTS 
    /// startup and shutdown (and maybe periodically in-between)
    /// </summary>
    public static class StartupTasks
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void CheckConfigs()
        {
            EmailContentManager.Check();

            //logs
            Console.WriteLine($"Logs - debug enabled: {_log.IsDebugEnabled}");
            Console.WriteLine($"Logs - error enabled: {_log.IsErrorEnabled}");
            Console.WriteLine($"Logs - fatal enabled: {_log.IsFatalEnabled}");
            Console.WriteLine($"Logs - info enabled: {_log.IsInfoEnabled}");
            Console.WriteLine($"Logs - trace enabled: {_log.IsTraceEnabled}");
            Console.WriteLine($"Logs - warn enabled: {_log.IsWarnEnabled}");
        }

        public static void CleanupProcesses()
        {
            try
            {
                var cleanupList = new List<string>
                {
                    ProcessManager.ProcessNames.ChromeDriver,
                    ProcessManager.ProcessNames.Word,
                    ProcessManager.ProcessNames.PowerPoint,
                    ProcessManager.ProcessNames.Excel,
                    ProcessManager.ProcessNames.Chrome,
                    ProcessManager.ProcessNames.Outlook,
                    ProcessManager.ProcessNames.Command,
                    ProcessManager.ProcessNames.PowerShell,
                    ProcessManager.ProcessNames.GeckoDriver,
                    ProcessManager.ProcessNames.Firefox,
                    ProcessManager.ProcessNames.WindowsFault,
                    ProcessManager.ProcessNames.WindowsFaultSecure
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
            catch(Exception e)
            {
                _log.Debug($"Cleanup process exception: {e}");
            }
        }

        public static void SetStartup()
        {
            try
            {
                var rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                rk?.SetValue(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name, Application.ExecutablePath);
                _log.Trace("Set startup registry key successfully");
            }
            catch (Exception e)
            {
                _log.Debug($"Could not set registry key for startup: {e}");
            }
        }
    }
}
