// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Infrastructure;

public static class ProcessManager
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static int GetThisProcessPid()
    {
        var currentProcess = Process.GetCurrentProcess();
        return currentProcess.Id;
    }

    public static void KillProcessAndChildrenByHandler(TimelineHandler handler)
    {
        _log.Trace($"Killing: {handler.HandlerType}...");
        switch (handler.HandlerType)
        {
            case HandlerType.BrowserChrome:
                KillProcessAndChildrenByName("chrome");
                KillProcessAndChildrenByName("chromedriver");
                break;
            case HandlerType.BrowserFirefox:
                KillProcessAndChildrenByName("firefox");
                KillProcessAndChildrenByName("geckodriver");
                break;
            case HandlerType.Command:
                KillProcessAndChildrenByName("cmd");
                break;
            case HandlerType.PowerShell:
                KillProcessAndChildrenByName("powershell");
                break;
            case HandlerType.Word:
                KillProcessAndChildrenByName("winword");
                break;
            case HandlerType.Excel:
                KillProcessAndChildrenByName("excel");
                break;
            case HandlerType.PowerPoint:
                KillProcessAndChildrenByName("powerpnt");
                break;
            case HandlerType.Outlook:
                KillProcessAndChildrenByName("outlook");
                break;

        }
    }

    public static void KillProcessAndChildrenByName(string procName)
    {
        if (!Program.Configuration.ResourceControl.ManageProcesses) return;
        try
        {
            var processes = Process.GetProcessesByName(procName).ToList();
            processes.Sort((x1, x2) => x1.StartTime.CompareTo(x2.StartTime));

            var thisPid = GetThisProcessPid();

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == thisPid) //don't kill thyself
                        continue;

                    process.SafeKill();
                }
                catch (Exception e)
                {
                    _log.Trace($"Closing {procName} threw exception - {e}");
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
        if (!Program.Configuration.ResourceControl.ManageProcesses) return;
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
                proc.SafeKill();
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
            var processes = Process.GetProcessesByName(processName);

            return processes.Select(proc => proc.Id).ToArray();
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