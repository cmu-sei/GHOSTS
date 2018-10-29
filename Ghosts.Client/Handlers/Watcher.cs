// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Ghosts.Domain.Code;
using NLog;
using System;
using System.IO;
using System.Threading;

namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Watcher is file only at the moment
    /// </summary>
    internal class Watcher : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public Watcher(TimelineHandler handler)
        {
            _log.Trace("Spawning watcher handler...");

            _log.Trace("Can't loop on watcher!...");

            try
            {
                Ex(handler);
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {
            foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                {
                    Thread.Sleep(timelineEvent.DelayBefore);
                }

                _log.Trace($"Watcher: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    //TODO:watch folders
                    //case "folder":
                    //    while (true)
                    //    {
                    //        var cmd = timelineEvent.CommandArgs[new Random().Next(0, timelineEvent.CommandArgs.Count)];
                    //        if (!string.IsNullOrEmpty(cmd))
                    //        {
                    //            this.Command(handler, timelineEvent, cmd);
                    //        }
                    //        Thread.Sleep(timelineEvent.DelayAfter);
                    //    }
                    //File
                    default:
                        FileWatcher w = new FileWatcher(handler, timelineEvent, timelineEvent.Command);
                        break;
                }

                if (timelineEvent.DelayAfter > 0)
                {
                    Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
        }
    }

    internal class FileWatcher : BaseHandler
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private TimelineHandler _handler;
        private TimelineEvent _timelineEvent;
        private readonly string _command;
        private readonly string _filePath;
        private static DateTime _lastRead = DateTime.MinValue;

        internal FileWatcher(TimelineHandler handler, TimelineEvent timelineEvent, string command)
        {
            _handler = handler;
            _timelineEvent = timelineEvent;
            _command = command;

            _filePath = timelineEvent.CommandArgs[0];
            var sleepTime = Convert.ToInt32(timelineEvent.CommandArgs[1]);

            if (string.IsNullOrEmpty(_filePath))
            {
                _log.Trace("file path null or empty");
                return;
            }

            var path = string.Empty;
            var file = string.Empty;

            var attr = File.GetAttributes(_filePath);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                path = _filePath;
                _log.Trace($"Directory passed: {path}");
            }
            else
            {
                //MessageBox.Show("Its a file");
                var f = new FileInfo(_filePath);
                path = f.DirectoryName;
                file = f.Name;
                _log.Trace($"File passed - Directory : {path} File: {file}");
            }
            
            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };

            if (!string.IsNullOrEmpty(file))
                watcher.Filter = file;

            watcher.EnableRaisingEvents = true;
            watcher.Changed += new FileSystemEventHandler(OnChanged);

            _log.Trace($"Setting up watcher for {_filePath} - watching {path} and filtering on {file}");

            // Begin watching.
            watcher.EnableRaisingEvents = true;
            
            while (true)
            {
                Thread.Sleep(sleepTime);
            }
        }

        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // filewatcher throws multiple events, we only need 1
            var lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime > _lastRead.AddSeconds(1))
            {
                _lastRead = lastWriteTime;
                _log.Trace("File: " + e.FullPath + " " + e.ChangeType);
                
                try
                {
                    var fileContents = string.Empty;
                    using (var logFileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var logFileReader = new StreamReader(logFileStream))
                        {
                            fileContents = logFileReader.ReadToEnd();
                        }
                    }
                    
                    this.Report(_handler.HandlerType.ToString(), _command, _filePath, _timelineEvent.TrackableId, fileContents);

                    if (Program.IsDebug)
                        Console.WriteLine($"File: {e.FullPath} : {e.ChangeType} : {fileContents}");
                }
                catch (Exception exception)
                {
                    _log.Error(exception);
                }
            }
        }
    }
}