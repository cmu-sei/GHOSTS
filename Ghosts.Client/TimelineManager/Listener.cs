// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using Ghosts.Domain;
using Newtonsoft.Json;
using NLog;
using SimpleTCP;
using Listener = Ghosts.Client.TimelineManager.Listener;

namespace Ghosts.Client.TimelineManager
{
    public static class ListenerManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static void Run()
        {
            try
            {
                if (Program.Configuration.Listener.Port > 0)
                {
                    var t = new Thread(() => { new Listener(); })
                    {
                        IsBackground = true,
                        Name = "ghosts-listener"
                    };
                    t.Start();
                }
            }
            catch (Exception e)
            {
                _log.Debug(e);
            }
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

                server.DataReceived += (sender, message) => { var obj = Handle(message); message.ReplyLine($"{obj}{Environment.NewLine}");  };
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
                
                foreach(var evs in timelineHandler.TimeLineEvents)
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
