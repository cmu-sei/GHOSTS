// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Ghosts.Client.Infrastructure.Email;
using Ghosts.Domain.Code;
using NLog;
using Microsoft.Win32;

namespace Ghosts.Client.Infrastructure;

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
        _log.Trace("Running process cleaner...");

        try
        {
            var cleanupList = new List<string>();

            var timeline = TimelineBuilder.GetTimeline();
            foreach (var handler in timeline.TimeLineHandlers)
            {
                cleanupList.AddRange(ProcessManager.GetProcessNames(handler.HandlerType));
            }
            
            //need to kill any other instance of ghosts already running
            var ghosts = Process.GetCurrentProcess();
            if (!Program.Configuration.AllowMultipleInstances)
            {
                cleanupList.Add(ghosts.ProcessName);
            }

            _log.Trace($"Found ghosts pid: {ghosts.Id}");

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
        catch(Exception e)
        {
            _log.Debug($"Cleanup process exception: {e}");
        }
    }

    public static void ConfigureStartup(bool isStartupDisabled)
    {
        if (isStartupDisabled)
        {
            RemoveStartup();
            
        }
        else
        {
            SetStartup();
        }
    }
    
    private static void SetStartup()
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

    private static void RemoveStartup()
    {
        try
        {
            var rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (rk?.GetValue(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name) != null)
            {
                rk?.DeleteValue(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
                _log.Trace("Removed startup registry key successfully");
            }
            else
            {
                _log.Trace("Startup registry key does not exist");
            }
        }
        catch (Exception e)
        {
            _log.Debug($"Could not remove registry key for startup: {e}");
        }
    }
}