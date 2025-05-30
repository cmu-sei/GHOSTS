// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Ghosts.Client.Infrastructure.Email;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
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
                    case HandlerType.Command:
                        cleanupList.Add(ProcessManager.ProcessNames.Command);
                        break;
                    case HandlerType.Outlook:
                        cleanupList.Add(ProcessManager.ProcessNames.Outlook);
                        break;
                    case HandlerType.Outlookv2:
                        cleanupList.Add(ProcessManager.ProcessNames.Outlook);
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
                }
            }
            
            //need to kill any other instance of ghosts already running
            var ghosts = Process.GetCurrentProcess();
            cleanupList.Add(ghosts.ProcessName);

            _log.Trace($"Found ghosts pid: {ghosts.Id}");

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
                                if (process.ProcessName == ApplicationDetails.Name &&
                                    Program.Configuration.AllowMultipleInstances == false)
                                {
                                    process.SafeKill();
                                }
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