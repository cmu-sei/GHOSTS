// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
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

    /// <summary>
    /// Instances started with "dotnet {assemblyName}.dll", which run as "dotnet"
    /// </summary>
    public static IEnumerable<int> GetDotnetHostedPids(string assemblyName)
    {
        var dll = $"{assemblyName}.dll";
        return Process.GetProcessesByName("dotnet")
            .Where(p => GetCommandLine(p.Id) is { } commandLine
                        && commandLine.Contains(dll, StringComparison.OrdinalIgnoreCase)
                        && !commandLine.Contains("--handle")) //a one-shot --handle run isn't another agent
            .Select(p => p.Id)
            .ToArray();
    }

    private static string GetCommandLine(int pid)
    {
        try
        {
            if (OperatingSystem.IsLinux())
                return File.ReadAllText($"/proc/{pid}/cmdline").Replace('\0', ' ');

            if (OperatingSystem.IsWindows())
            {
                using var searcher = new ManagementObjectSearcher($"Select CommandLine From Win32_Process Where ProcessId={pid}");
                foreach (var mo in searcher.Get())
                    return mo["CommandLine"]?.ToString();
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return null;
    }

    public static void KillProcessAndChildrenByHandler(TimelineHandler handler)
    {
        _log.Trace($"Killing: {handler.HandlerType}...");
        foreach (var processName in GetProcessNames(handler.HandlerType))
        {
            KillProcessAndChildrenByName(processName);
        }
    }

    /// <summary>
    /// The processes a handler runs, which cleanup closes
    /// </summary>
    public static string[] GetProcessNames(HandlerType handlerType) => handlerType switch
    {
        HandlerType.BrowserChrome => [ProcessNames.Chrome, ProcessNames.ChromeDriver],
        HandlerType.BrowserEdge => [ProcessNames.MSEdge, ProcessNames.MSEdgeDriver],
        HandlerType.BrowserFirefox => [ProcessNames.Firefox, ProcessNames.GeckoDriver],
        HandlerType.Word => [ProcessNames.Word],
        HandlerType.Excel => [ProcessNames.Excel],
        HandlerType.PowerPoint => [ProcessNames.PowerPoint],
        HandlerType.Command => [ProcessNames.Command],
        HandlerType.PowerShell => [ProcessNames.PowerShell],
        HandlerType.Curl => [ProcessNames.Curl],
        _ => [] //includes Outlook/Outlookv2, which send over MailKit and run no process
    };

    public static class ProcessNames
    {
        public static string Chrome => "chrome";
        public static string ChromeDriver => "chromedriver";
        public static string MSEdge => "msedge";
        public static string MSEdgeDriver => "msedgedriver";
        public static string Command => "cmd"; //the Command handler always runs cmd; bash comes from the Bash handler, never kill it by name
        public static string PowerShell => "powershell";
        public static string Firefox => "firefox";
        public static string GeckoDriver => "geckodriver";
        public static string Excel => "EXCEL";
        public static string PowerPoint => "POWERPNT";
        public static string Word => "WINWORD";
        public static string Curl => "curl";
    }
}
