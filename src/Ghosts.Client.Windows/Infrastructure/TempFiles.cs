// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using NLog;

namespace Ghosts.Client.Infrastructure;

public class TempFiles
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static void StartTempFileWatcher()
    {
        try
        {
            var t = new Thread(TempFileWatcher)
            {
                IsBackground = true,
                Name = "ghosts-tempfoldercleanup"
            };
            t.Start();
        }
        catch (Exception e)
        {
            _log.Error($"TempFileWatcher thread launch exception: {e}");
        }
    }

    private static void TempFileWatcher()
    {
        while (true)
        {
            try
            {
                _log.Trace("TempFileWatcher loop beginning");
                CleanUpTempFolder();
                _log.Trace("TempFileWatcher loop ending");
            }
            catch (Exception e)
            {
                _log.Trace($"TempFileWatcher exception: {e}");
            }
            finally
            {
                Thread.Sleep(300000); //every 5 minutes clean up
            }
        }
    }

    private static void CleanUpTempFolder()
    {
        try
        {
            var di = new DirectoryInfo(Path.GetTempPath());

            foreach (var file in di.EnumerateFiles())
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    //
                }
            }
            foreach (var dir in di.EnumerateDirectories())
            {
                try
                {
                    dir.Delete(true);
                }
                catch
                {
                    //
                }
            }
        }
        catch (Exception e)
        {
            _log.Error($"Error deleting temp files {e}");
        }
    }
}