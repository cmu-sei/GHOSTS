// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure;

public static class TempFiles
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static void StartTempFileWatcher()
    {
        var t = new Thread(TempFileWatcher)
        {
            IsBackground = true,
            Name = "ghosts-tempfoldercleanup"
        };
        t.Start();
    }

    private static void TempFileWatcher()
    {
        while (true)
        {
            CleanUpTempFolder();
            Thread.Sleep(300000);
        }
    }

    private static void CleanUpTempFolder()
    {
        var tempPath = Path.GetTempPath();

        try
        {
            foreach (var file in Directory.GetFiles(tempPath))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception e)
                {
                    _log.Trace($"Could not delete temp file {file}: {e}");
                }
            }

            foreach (var dir in Directory.GetDirectories(tempPath))
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (Exception e)
                {
                    _log.Trace($"Could not delete temp directory {dir}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            _log.Trace($"Could not enumerate temp folder {tempPath}: {e}");
        }
    }
}
