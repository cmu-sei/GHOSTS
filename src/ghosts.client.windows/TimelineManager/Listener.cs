// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Newtonsoft.Json;
using NLog;
using System;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code;

namespace Ghosts.Client.TimelineManager;

public static class ListenerManager
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    internal static string In = ApplicationDetails.InstanceDirectories.TimelineIn;
    internal static string Out = ApplicationDetails.InstanceDirectories.TimelineOut;

    public static void Run()
    {
        try
        {
            if (!string.IsNullOrEmpty(In))
            {
                if (!Directory.Exists(In))
                {
                    Directory.CreateDirectory(In);
                    _log.Trace($"DirectoryListener created DirIn: {In})");
                }

                if (!Directory.Exists(Out))
                {
                    Directory.CreateDirectory(Out);
                    _log.Trace($"DirectoryListener created DirIn: {Out})");
                }

                var _ = new DirectoryListener();
            }
            else
            {
                _log.Trace("DirectoryListener is not configured (DirIn or DirOut is blank)");
            }
        }
        catch (Exception e)
        {
            _log.Debug(e);
        }
    }
}

/// <summary>
/// Watches a directory [ghosts install]\instance\timeline for dropped files, and processes them immediately
/// </summary>
public class DirectoryListener
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly string _in = ListenerManager.In;
    private static readonly string _out = ListenerManager.Out;
    private static string _currentlyProcessing = string.Empty;

    public DirectoryListener()
    {
        var watcher = new FileSystemWatcher
        {
            Path = _in,
            NotifyFilter = NotifyFilters.LastWrite,
            Filter = "*.*"
        };
        watcher.Changed += OnChanged;
        watcher.EnableRaisingEvents = true;
    }

    private void OnChanged(object source, FileSystemEventArgs e)
    {
        // filewatcher throws multiple events, we only need 1
        if (!string.IsNullOrEmpty(_currentlyProcessing) && _currentlyProcessing == e.FullPath) return;
        _currentlyProcessing = e.FullPath;

        _log.Trace("DirectoryListener found file: " + e.FullPath + " " + e.ChangeType);

        if (!File.Exists(e.FullPath))
            return;

        if (e.FullPath.EndsWith(".json"))
        {
            try
            {
                var timeline = TimelineBuilder.GetTimeline(e.FullPath);
                if (timeline is null)
                    return;

                foreach (var timelineHandler in timeline.TimeLineHandlers)
                {
                    _log.Trace($"DirectoryListener command found: {timelineHandler.HandlerType}");

                    foreach (var timelineEvent in timelineHandler.TimeLineEvents)
                    {
                        if (string.IsNullOrEmpty(timelineEvent.TrackableId))
                        {
                            timelineEvent.TrackableId = Guid.NewGuid().ToString();
                        }
                    }

                    var orchestrator = new Orchestrator();
                    orchestrator.RunCommand(timeline, timelineHandler);
                }
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
            }
        }
        else if (e.FullPath.EndsWith(".cs"))
        {
            try
            {
                var commands = File.ReadAllText(e.FullPath).Split(Convert.ToChar("\n")).ToList();
                if (commands.Count > 0)
                {
                    var constructedTimelineHandler = TimelineTranslator.FromBrowserUnitTests(commands);
                    var orchestrator = new Orchestrator();
                    var t = new Timeline
                    {
                        Id = Guid.NewGuid(),
                        Status = Timeline.TimelineStatus.Run
                    };
                    t.TimeLineHandlers.Add(constructedTimelineHandler);
                    orchestrator.RunCommand(t, constructedTimelineHandler);
                }
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
            }
        }

        try
        {
            var outfile = e.FullPath.Replace(_in, _out);
            outfile = outfile.Replace(e.Name, $"{DateTime.Now.ToString("G").Replace("/", "-").Replace(" ", "").Replace(":", "")}-{e.Name}");

            File.Move(e.FullPath, outfile);
        }
        catch (Exception exception)
        {
            _log.Debug(exception);
        }

        _currentlyProcessing = string.Empty;
    }
}