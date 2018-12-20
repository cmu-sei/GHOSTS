// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Newtonsoft.Json;
using NLog;
using SimpleTCP;
using System;
using System.IO;
using System.Threading;
using Ghosts.Domain.Code;

namespace Ghosts.Client.TimelineManager
{
    public static class ListenerManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        internal static string In = ApplicationDetails.InstanceDirectories.Timeline;

        public static void Run()
        {
            try
            {
                if (Program.Configuration.Listener.Port > 0)
                {
                    Thread t = new Thread(() => { new PortListener(); })
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

    public class PortListener
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public PortListener()
        {
            try
            {
                SimpleTcpServer server = new SimpleTcpServer().Start(Program.Configuration.Listener.Port);
                server.AutoTrimStrings = true;
                server.Delimiter = 0x13;

                Console.WriteLine(
                    $"PortListener active on {string.Join(",", server.GetListeningIPs())} : {Program.Configuration.Listener.Port}");

                server.DataReceived += (sender, message) =>
                {
                    string obj = Handle(message);
                    message.ReplyLine($"{obj}{Environment.NewLine}");
                };
            }
            catch (Exception e)
            {
                _log.Trace(e);
                Console.WriteLine($"PortListener could not be started on {Program.Configuration.Listener.Port}");
            }
        }

        private string Handle(Message message)
        {
            string tempMsg =
                $"PortListener received raw {message.TcpClient.Client.RemoteEndPoint}: {message.MessageString}";
            Console.WriteLine(tempMsg);
            _log.Trace(tempMsg);

            string command = message.MessageString;
            int index = command.LastIndexOf("}", StringComparison.InvariantCultureIgnoreCase);
            if (index > 0)
            {
                command = command.Substring(0, index + 1);
            }

            _log.Trace($"PortListener received from {message.TcpClient.Client.RemoteEndPoint}: {command}");

            try
            {
                TimelineHandler timelineHandler = JsonConvert.DeserializeObject<TimelineHandler>(command);

                foreach (TimelineEvent evs in timelineHandler.TimeLineEvents)
                {
                    if (string.IsNullOrEmpty(evs.TrackableId))
                    {
                        evs.TrackableId = Guid.NewGuid().ToString();
                    }
                }

                _log.Trace($"PortListener command found: {timelineHandler.HandlerType}");

                Orchestrator o = new Orchestrator();
                o.RunCommand(timelineHandler);

                string obj = JsonConvert.SerializeObject(timelineHandler);

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