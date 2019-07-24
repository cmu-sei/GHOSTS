// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace Ghosts.Client.Infrastructure
{
    public static class ProcessManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static int Jitter(int baseSleep)
        {
            //sleep with jitter
            var sleep = baseSleep;
            var r = new Random().Next(-999, 1999);
            sleep += r;
            if (sleep < 0)
                sleep = 1;
            return sleep;
        }

        public static int GetThisProcessPid()
        {
            var currentProcess = Process.GetCurrentProcess();
            return currentProcess.Id;
        }

        public static void KillProcessAndChildrenByName(string procName)
        {
            try
            {
                var procs = Process.GetProcessesByName(procName).ToList();
                procs.Sort((x1, x2) => x1.StartTime.CompareTo(x2.StartTime));

                var thisPid = GetThisProcessPid();
                
                foreach (var process in procs)
                {
                    try
                    {
                        if (process.Id == thisPid) //don't kill thyself
                            continue;

                        process.Kill();
                        process.WaitForExit();
                        _log.Trace($"Successfully killed {procName}");
                    }
                    catch (Exception e)
                    {
                        _log.Trace($"Killing {procName} threw exception - {e}");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Trace($"Could not get processes by name? {procName} : {e}");
            }
        }

        public static void KillProcessAndChildrenByPid(int pid)
        {
            try
            {
                
                if (pid == 0) // Cannot close 'system idle process'.
                    return;

                if (pid == GetThisProcessPid()) //don't kill thyself
                    return;

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
                catch (Exception e)
                {
                    _log.Trace(e);
                }
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
        }

        public static IEnumerable<int> GetPids(string processName)
        {
            try
            {
                Process[] procs = Process.GetProcessesByName(processName);

                return procs.Select(proc => proc.Id).ToArray();
            }
            catch (Exception e)
            {
                _log.Trace(e);
                return new List<int>();
            }
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
