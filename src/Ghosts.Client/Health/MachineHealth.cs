// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Health;

public static class MachineHealth
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static ResultHealth.MachineStats Run()
    {
        var stats = new ResultHealth.MachineStats();
        try
        {
            stats.Memory = GetMemory();
            stats.Cpu = GetCpu();
            stats.DiskSpace = GetDiskSpace();
            _log.Trace($"MEMORY: {stats.Memory} CPU: {stats.Cpu} DISK: {stats.DiskSpace}");
        }
        catch (Exception ex)
        {
            _log.Debug($"Health tasks failing: {ex}");
        }
        return stats;
    }

    public static float GetMemory()
    {
        var c = new Microsoft.VisualBasic.Devices.ComputerInfo();
        return 1 - Convert.ToSingle(c.AvailablePhysicalMemory) / Convert.ToSingle(c.TotalPhysicalMemory);
    }

    public static float GetCpu()
    {
        var o = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        o.NextValue();
        Thread.Sleep(1000);
        return o.NextValue();
    }

    public static float GetDiskSpace()
    {
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady && drive.Name == Path.GetPathRoot(ApplicationDetails.InstalledPath))
            {
                return 1 - Convert.ToSingle(drive.AvailableFreeSpace) / Convert.ToSingle(drive.TotalSize);
            }
        }
        return -1;
    }
}