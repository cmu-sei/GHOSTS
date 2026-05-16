// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Universal.Handlers;

internal class Watcher(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override async Task RunOnce()
    {
        var watchers = new List<FileSystemWatcher>();
        try
        {
            foreach (var timelineEvent in Handler.TimeLineEvents)
            {
                Token.ThrowIfCancellationRequested();
                WorkingHours.Is(Handler);

                _log.Trace($"Watcher: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    case "folder":
                        SetupFolderWatcher(timelineEvent, watchers);
                        break;
                    default:
                        SetupFileWatcher(timelineEvent, watchers);
                        break;
                }
            }

            while (true)
            {
                if (Token.WaitHandle.WaitOne(2000)) Token.ThrowIfCancellationRequested();  //default
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        finally
        {
            foreach (var w in watchers)
            {
                try { w.Dispose(); }
                catch { }
            }
        }
    }

    private void SetupFolderWatcher(TimelineEvent timelineEvent, List<FileSystemWatcher> watchers)
    {
        string folderPath = null;
        long folderMaxSizeBytes = -1;
        var deletionApproach = "random";

        foreach (var cmd in timelineEvent.CommandArgs)
        {
            var argString = cmd?.ToString();
            if (string.IsNullOrEmpty(argString)) continue;

            var parts = argString.Split(new[] { ':' }, 2, StringSplitOptions.None);
            if (parts.Length != 2) continue;

            switch (parts[0])
            {
                case "path":
                    folderPath = Environment.ExpandEnvironmentVariables(parts[1]);
                    break;
                case "size":
                    if (long.TryParse(parts[1], out var sizeMb))
                        folderMaxSizeBytes = sizeMb * 1024 * 1024;
                    break;
                case "deletionApproach":
                    deletionApproach = parts[1];
                    break;
            }
        }

        if (string.IsNullOrEmpty(folderPath))
        {
            _log.Trace("Watcher:: No 'path' argument specified for folder watcher, skipping.");
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            _log.Trace($"Watcher:: Folder path '{folderPath}' does not exist, skipping.");
            return;
        }

        if (folderMaxSizeBytes < 0)
        {
            _log.Trace("Watcher:: No valid 'size' argument specified for folder watcher, skipping.");
            return;
        }

        if (deletionApproach != "random" && deletionApproach != "oldest" && deletionApproach != "largest")
        {
            _log.Trace($"Watcher:: Unrecognized deletionApproach '{deletionApproach}', using random.");
            deletionApproach = "random";
        }

        var capturedPath = folderPath;
        var capturedMaxSize = folderMaxSizeBytes;
        var capturedApproach = deletionApproach;

        var watcher = new FileSystemWatcher(folderPath)
        {
            NotifyFilter = NotifyFilters.Size,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };

        watcher.Changed += (_, _) => OnFolderChanged(capturedPath, capturedMaxSize, capturedApproach);

        watchers.Add(watcher);
        _log.Trace($"Watcher:: Set up folder watcher for '{folderPath}' with maxSize {folderMaxSizeBytes} bytes, approach={deletionApproach}.");
    }

    private void OnFolderChanged(string folderPath, long maxSizeBytes, string deletionApproach)
    {
        try
        {
            var currentSize = GetDirectorySize(folderPath);
            if (currentSize <= maxSizeBytes) return;

            var allFiles = GetAllFiles(folderPath);
            if (allFiles.Count == 0) return;

            switch (deletionApproach)
            {
                case "oldest":
                    allFiles.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));
                    break;
                case "largest":
                    allFiles.Sort((a, b) => b.Length.CompareTo(a.Length));
                    break;
            }

            while (currentSize > maxSizeBytes && allFiles.Count > 0)
            {
                FileInfo target;
                if (deletionApproach == "random")
                {
                    var idx = _random.Next(allFiles.Count);
                    target = allFiles[idx];
                    allFiles.RemoveAt(idx);
                }
                else
                {
                    target = allFiles[0];
                    allFiles.RemoveAt(0);
                }

                try
                {
                    var size = target.Length;
                    File.Delete(target.FullName);
                    currentSize -= size;
                    _log.Trace($"Watcher:: Deleted {target.FullName} to reduce folder '{folderPath}' size.");

                    Report(new ReportItem
                    {
                        Handler = Handler.HandlerType.ToString(),
                        Command = "folder-delete",
                        Arg = target.FullName
                    });
                }
                catch (Exception ex)
                {
                    _log.Trace($"Watcher:: Unable to delete {target.FullName}: {ex.Message}");
                }
            }

            if (currentSize <= maxSizeBytes)
                _log.Trace($"Watcher:: Successfully reduced folder '{folderPath}' to target size.");
        }
        catch (Exception e)
        {
            _log.Trace("Watcher:: Unexpected exception in folder change handler.");
            _log.Error(e);
        }
    }

    private void SetupFileWatcher(TimelineEvent timelineEvent, List<FileSystemWatcher> watchers)
    {
        if (timelineEvent.CommandArgs == null || timelineEvent.CommandArgs.Count == 0)
        {
            _log.Trace("Watcher:: No file path specified, skipping.");
            return;
        }

        var filePath = timelineEvent.CommandArgs[0]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            _log.Trace("Watcher:: File path is null or empty, skipping.");
            return;
        }

        filePath = Environment.ExpandEnvironmentVariables(filePath);

        string directory;
        string filter;

        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            directory = fileInfo.DirectoryName;
            filter = fileInfo.Name;
        }
        else if (Directory.Exists(filePath))
        {
            directory = filePath;
            filter = "*";
        }
        else
        {
            _log.Trace($"Watcher:: Path '{filePath}' does not exist, skipping.");
            return;
        }

        var capturedFilePath = filePath;
        var capturedTrackable = timelineEvent.TrackableId;

        var watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size |
                           NotifyFilters.CreationTime | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        if (filter != "*")
            watcher.Filter = filter;

        var lastRead = DateTime.MinValue;
        watcher.Changed += (_, e) =>
        {
            var lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime <= lastRead.AddSeconds(1)) return;
            lastRead = lastWriteTime;

            _log.Trace($"Watcher:: File changed: {e.FullPath} ({e.ChangeType})");
            Report(new ReportItem
            {
                Handler = Handler.HandlerType.ToString(),
                Command = "file-changed",
                Arg = e.FullPath,
                Trackable = capturedTrackable
            });
        };

        watchers.Add(watcher);
        _log.Trace($"Watcher:: Set up file watcher for '{capturedFilePath}' (dir={directory}, filter={filter}).");
    }

    private static long GetDirectorySize(string folder)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; }
                catch { }
            }
        }
        catch { }
        return size;
    }

    private static List<FileInfo> GetAllFiles(string folder)
    {
        var result = new List<FileInfo>();
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try { result.Add(new FileInfo(file)); }
                catch { }
            }
        }
        catch { }
        return result;
    }
}
