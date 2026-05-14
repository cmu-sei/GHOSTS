// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ghosts.Domain;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure;

public static class ProcessManager
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static int GetThisProcessPid()
    {
        var currentProcess = Process.GetCurrentProcess();
        return currentProcess.Id;
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

    public static void KillProcessAndChildrenByName(string procName)
    {
        if (Program.Configuration?.ResourceControl?.ManageProcesses == false) return;

        try
        {
            var processes = Process.GetProcessesByName(procName).ToList();
            var thisPid = GetThisProcessPid();

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id == thisPid)
                        continue;

                    process.Kill(true);
                }
                catch (Exception e)
                {
                    _log.Debug($"Closing {procName} threw exception - {e}");
                }
            }
        }
        catch (Exception e)
        {
            _log.Debug($"Could not get processes by name? {procName} : {e}");
        }
    }

    public static void KillProcessAndChildrenByPid(int pid)
    {
        if (Program.Configuration?.ResourceControl?.ManageProcesses == false) return;

        try
        {
            if (pid == 0)
                return;

            if (pid == GetThisProcessPid())
                return;

            var proc = Process.GetProcessById(pid);
            proc.Kill(true);
        }
        catch (Exception e)
        {
            _log.Debug($"Could not kill process by pid {pid} : {e}");
        }
    }

    public static void KillProcessAndChildrenByHandler(TimelineHandler handler)
    {
        _log.Trace($"Killing: {handler.HandlerType}...");
        switch (handler.HandlerType)
        {
            case HandlerType.BrowserChrome:
                KillProcessAndChildrenByName(ProcessNames.Chrome);
                KillProcessAndChildrenByName(ProcessNames.ChromeDriver);
                break;
            case HandlerType.BrowserFirefox:
                KillProcessAndChildrenByName(ProcessNames.Firefox);
                KillProcessAndChildrenByName(ProcessNames.GeckoDriver);
                break;
            case HandlerType.Word:
                KillProcessAndChildrenByName(ProcessNames.Word);
                break;
            case HandlerType.Excel:
                KillProcessAndChildrenByName(ProcessNames.Excel);
                break;
            case HandlerType.PowerPoint:
                KillProcessAndChildrenByName(ProcessNames.PowerPoint);
                break;
            case HandlerType.Outlook:
                KillProcessAndChildrenByName(ProcessNames.Outlook);
                break;
            case HandlerType.Command:
                KillProcessAndChildrenByName(ProcessNames.Command);
                break;
            case HandlerType.PowerShell:
                KillProcessAndChildrenByName(ProcessNames.PowerShell);
                break;
            case HandlerType.Curl:
                KillProcessAndChildrenByName(ProcessNames.Curl);
                break;
        }
    }

    public static class ProcessNames
    {
        public static string Chrome => "chrome";
        public static string ChromeDriver => "chromedriver";
        public static string Command => OperatingSystem.IsWindows() ? "cmd" : "bash";
        public static string PowerShell => "powershell";
        public static string Firefox => "firefox";
        public static string GeckoDriver => "geckodriver";
        public static string Excel => "EXCEL";
        public static string Outlook => "OUTLOOK";
        public static string PowerPoint => "POWERPNT";
        public static string Word => "WINWORD";
        public static string Curl => "curl";
    }
}
