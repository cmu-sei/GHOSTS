// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using NLog;

namespace Ghosts.Client.Code
{
    public static class ProcessManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void KillProcessAndChildrenByName(string procName)
        {
            try
            {
                foreach (var pid in GetPids(procName))
                {
                    KillProcessAndChildrenByPid(pid);
                }
            }
            catch (Exception e)
            {
                _log.Trace($"Killing {procName} failed - {e}");
            }
        }

        public static void KillProcessAndChildrenByPid(int pid)
        {
            // Cannot close 'system idle process'.
            if (pid == 0)
            {
                return;
            }
            var searcher = new ManagementObjectSearcher($"Select * From Win32_Process Where ParentProcessID={pid}");
            var moc = searcher.Get();
            foreach (var mo in moc)
            {
                KillProcessAndChildrenByPid(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                var proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch
            {
                // Process already exited.
            }
        }

        public static IEnumerable<int> GetPids(string processName)
        {
            var procs = Process.GetProcessesByName(processName);

            return procs.Select(proc => proc.Id).ToArray();
        }

        public static IEnumerable<int> GetPidsWithUi(string processName)
        {
            var pids = new List<int>();
            var procs = Process.GetProcessesByName(processName);
            foreach (var proc in procs)
            {
                if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                {
                    pids.Add(proc.Id);
                }
            }

            return pids.ToArray();
        }

        public static class ProcessNames
        {
            public static string Chrome => "chrome";
            public static string ChromeDriver => "chromedriver";

            public static string Command => "cmd";
            public static string PowerShell => "powershell";

            public static string Firefox => "firefox";
            public static string GeckoDriver => "geckodriver";

            public static string Excel => "EXCEL";
            public static string Outlook => "OUTLOOK";
            public static string PowerPoint => "POWERPNT";
            public static string Word => "WINWORD";

            public static string WindowsFault => "werfault";
            public static string WindowsFaultSecure => "werfaultsecure";
        }
    }
}

