// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Universal.Infrastructure;

public static class TempFiles
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The ghosts-owned subfolder of the system temp path. Handler artifacts belong here so that
    /// cleanup never has to touch the shared temp folder, which on Linux is /tmp and holds sockets
    /// and state belonging to other users and services (see issue #698).
    /// </summary>
    public static string GetGhostsTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "ghosts");
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            _log.Debug($"TempFiles: could not create {path}: {e.Message}");
        }

        return path;
    }

    public static void StartTempFileWatcher()
    {
        var settings = Program.Configuration?.TempFiles ?? new ClientConfiguration.TempFilesSettings();
        if (!settings.IsEnabled)
        {
            _log.Trace("Temp file cleanup disabled, continuing.");
            return;
        }

        var path = string.IsNullOrWhiteSpace(settings.Path)
            ? GetGhostsTempPath()
            : Environment.ExpandEnvironmentVariables(settings.Path);
        var cycleSleep = settings.CycleSleepMinutes > 0 ? settings.CycleSleepMinutes : 5;
        var minimumAge = settings.MinimumAgeInMinutes > 0 ? settings.MinimumAgeInMinutes : 0;

        _log.Trace($"Temp file cleanup enabled for {path} every {cycleSleep}m, sparing anything newer than {minimumAge}m");

        var t = new Thread(() => TempFileWatcher(path, cycleSleep, minimumAge))
        {
            IsBackground = true,
            Name = "ghosts-tempfoldercleanup"
        };
        t.Start();
    }

    private static void TempFileWatcher(string path, int cycleSleepMinutes, int minimumAgeInMinutes)
    {
        while (true)
        {
            CleanUpTempFolder(path, minimumAgeInMinutes);
            Thread.Sleep(cycleSleepMinutes * 60 * 1000);
        }
    }

    /// <summary>
    /// Deletes aged entries under path. Failures are counted and summarized in a single line per
    /// cycle: a client without permission to a configured folder would otherwise log one error per
    /// file per cycle and bury everything else in the log.
    /// </summary>
    private static void CleanUpTempFolder(string path, int minimumAgeInMinutes)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minimumAgeInMinutes);
        var deleted = 0;
        var skipped = 0;
        string lastError = null;

        try
        {
            var di = new DirectoryInfo(path);

            foreach (var file in di.EnumerateFiles())
            {
                if (file.LastWriteTimeUtc > cutoff)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    file.Delete();
                    deleted++;
                }
                catch (Exception e)
                {
                    skipped++;
                    lastError = $"{file.Name}: {e.Message}";
                }
            }

            foreach (var dir in di.EnumerateDirectories())
            {
                if (dir.LastWriteTimeUtc > cutoff)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    dir.Delete(true);
                    deleted++;
                }
                catch (Exception e)
                {
                    skipped++;
                    lastError = $"{dir.Name}: {e.Message}";
                }
            }
        }
        catch (Exception e)
        {
            _log.Debug($"TempFiles: could not enumerate {path}: {e.Message}");
            return;
        }

        if (deleted > 0 || lastError != null)
        {
            var error = lastError == null ? string.Empty : $", last error: {lastError}";
            _log.Debug($"TempFiles: {path} - {deleted} deleted, {skipped} skipped{error}");
        }
    }
}
