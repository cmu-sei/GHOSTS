// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using SimpleTCP;
// ReSharper disable ObjectCreationAsStatement

namespace ghosts.client.linux.timelineManager
{
    public static class ListenerManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        internal static string In = ApplicationDetails.InstanceDirectories.TimelineIn;
        internal static string Out = ApplicationDetails.InstanceDirectories.TimelineOut;

        public static void Run()
        {
            try
            {
                if (Program.Configuration.Listener.Port > 0)
                {
                    var _ = new PortListener();
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }

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
                    var raw = File.ReadAllText(e.FullPath);

                    var timeline = JsonConvert.DeserializeObject<Timeline>(raw);

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

                        Orchestrator.RunCommand(timeline, timelineHandler);
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
                        var t = new Timeline
                        {
                            Id = Guid.NewGuid(),
                            Status = Timeline.TimelineStatus.Run
                        };
                        t.TimeLineHandlers.Add(constructedTimelineHandler);
                        Orchestrator.RunCommand(t, constructedTimelineHandler);
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

    public class PortListener
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public PortListener()
        {
            try
            {
                var server = new SimpleTcpServer().Start(Program.Configuration.Listener.Port);
                server.AutoTrimStrings = true;
                server.Delimiter = 0x13;

                Console.WriteLine(
                    $"PortListener active on {string.Join(",", server.GetListeningIPs())} : {Program.Configuration.Listener.Port}");

                server.DataReceived += (sender, message) =>
                {
                    var obj = Handle(message);
                    message.ReplyLine($"{obj}{Environment.NewLine}");
                };
            }
            catch (Exception e)
            {
                _log.Trace(e);
                Console.WriteLine($"PortListener could not be started on {Program.Configuration.Listener.Port}");
            }
        }

        private static string Handle(Message message)
        {
            var tempMsg =
                $"PortListener received raw {message.TcpClient.Client.RemoteEndPoint}: {message.MessageString}";
            Console.WriteLine(tempMsg);
            _log.Trace(tempMsg);

            var command = message.MessageString;
            var index = command.LastIndexOf("}", StringComparison.InvariantCultureIgnoreCase);
            if (index > 0)
            {
                command = command.Substring(0, index + 1);
            }

            _log.Trace($"PortListener received from {message.TcpClient.Client.RemoteEndPoint}: {command}");

            try
            {
                var timelineHandler = JsonConvert.DeserializeObject<TimelineHandler>(command) ?? throw new DataMisalignedException(
                        "Portlistener received something that could not be interpreted as a timeline");
                foreach (var evs in timelineHandler.TimeLineEvents)
                {
                    if (string.IsNullOrEmpty(evs.TrackableId))
                    {
                        evs.TrackableId = Guid.NewGuid().ToString();
                    }
                }

                _log.Trace($"PortListener command found: {timelineHandler.HandlerType}");

                var t = new Timeline
                {
                    Id = Guid.NewGuid(),
                    Status = Timeline.TimelineStatus.Run
                };
                t.TimeLineHandlers.Add(timelineHandler);

                Orchestrator.RunCommand(t, timelineHandler);

                var obj = JsonConvert.SerializeObject(timelineHandler);

                return obj;
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }

            return null;
        }
    }
}
