// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;
using SimpleTCP;

namespace ghosts.client.linux.timelineManager
{
    public static class ListenerManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        internal static string In = ApplicationDetails.InstanceDirectories.TimelineIn;

        public static void Run()
        {
            try
            {
                if (Program.Configuration.Listener.Port > 0)
                {
                    var t = new Thread(() => { new Listener(); })
                    {
                        IsBackground = true,
                        Name = "ghosts-portlistener"
                    };
                    t.Start();
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

                    var t = new Thread(() => { new DirectoryListener(); })
                    {
                        IsBackground = true,
                        Name = "ghosts-directorylistener"
                    };
                    t.Start();
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
        private static string _in = ListenerManager.In;
        private static string _currentlyProcessing = string.Empty;

        public DirectoryListener()
        {
            var watcher = new FileSystemWatcher
            {
                Path = _in,
                NotifyFilter = NotifyFilters.LastWrite,
                Filter = "*.json"
            };
            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // filewatcher throws multiple events, we only need 1
            if (!string.IsNullOrEmpty(_currentlyProcessing) && _currentlyProcessing == e.FullPath) return;
            _currentlyProcessing = e.FullPath;

            _log.Trace("DirectoryListener found file: " + e.FullPath + " " + e.ChangeType);

            try
            {
                var raw = File.ReadAllText(e.FullPath);

                var timeline = JsonConvert.DeserializeObject<Timeline>(raw);

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
                    orchestrator.RunCommand(timelineHandler);
                }

                File.Move(e.FullPath, e.FullPath.Replace(".json", $"-{Guid.NewGuid().ToString()}.processed"));
            }
            catch (Exception exc)
            {
                _log.Debug(exc);
            }

            _currentlyProcessing = string.Empty;
        }
    }

    public class Listener
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public Listener()
        {
            try
            {
                var server = new SimpleTcpServer().Start(Program.Configuration.Listener.Port);
                server.AutoTrimStrings = true;
                server.Delimiter = 0x13;

                Console.WriteLine($"Listener active on {string.Join(",", server.GetListeningIPs())} : {Program.Configuration.Listener.Port}");

                server.DataReceived += (sender, message) =>
                {
                    var obj = Handle(message);
                    message.ReplyLine($"{obj}{Environment.NewLine}");
                };
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
        }

        private string Handle(Message message)
        {
            var command = message.MessageString;
            var index = command.LastIndexOf("}", StringComparison.InvariantCultureIgnoreCase);
            if (index > 0)
                command = command.Substring(0, index + 1);

            _log.Trace($"Received from {message.TcpClient.Client.RemoteEndPoint}: {command}");

            try
            {
                var timelineHandler = JsonConvert.DeserializeObject<TimelineHandler>(command);

                foreach (var evs in timelineHandler.TimeLineEvents)
                    if (string.IsNullOrEmpty(evs.TrackableId))
                        evs.TrackableId = Guid.NewGuid().ToString();

                _log.Trace($"Command found: {timelineHandler.HandlerType}");

                var o = new Orchestrator();
                o.RunCommand(timelineHandler);

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