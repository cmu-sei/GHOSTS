// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using Ghosts.Domain.Code;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Watcher is file only at the moment
    /// </summary>
    internal class Watcher : BaseHandler
    {
        public Watcher(TimelineHandler handler)
        {
            base.Init(handler);
            Log.Trace("Spawning watcher handler...");

            Log.Trace("Can't loop on watcher!...");

            try
            {
                Ex(handler);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {
            foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
            {
                Infrastructure.WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                {
                    Thread.Sleep(timelineEvent.DelayBefore);
                }

                Log.Trace($"Watcher: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    //TODO:watch folders
                    //case "folder":
                    //    while (true)
                    //    {
                    //        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                    //        if (!string.IsNullOrEmpty(cmd))
                    //        {
                    //            this.Command(handler, timelineEvent, cmd);
                    //        }
                    //        Thread.Sleep(timelineEvent.DelayAfter);
                    //    }
                    //File
                    default:
                        var w = new FileWatcher(handler, timelineEvent, timelineEvent.Command);
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

            _filePath = timelineEvent.CommandArgs[0].ToString();
            var sleepTime = Convert.ToInt32(timelineEvent.CommandArgs[1]);

            if (timelineEvent.CommandArgs[2] != null)
            {
                var webhookPayload = timelineEvent.CommandArgs[2].ToString();
                this.WebhookCreate(webhookPayload);
            }
            
            if (string.IsNullOrEmpty(_filePath))
            {
                Log.Trace("file path null or empty");
                return;
            }

            var path = string.Empty;
            var file = string.Empty;

            var attr = File.GetAttributes(_filePath);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                path = _filePath;
                Log.Trace($"Directory passed: {path}");
            }
            else
            {
                //MessageBox.Show("Its a file");
                var f = new FileInfo(_filePath);
                path = f.DirectoryName;
                file = f.Name;
                Log.Trace($"File passed - Directory : {path} File: {file}");
            }
            
            var watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite
            };

            if (!string.IsNullOrEmpty(file))
                watcher.Filter = file;

            watcher.EnableRaisingEvents = true;
            watcher.Changed += OnChanged;

            Log.Trace($"Setting up watcher for {_filePath} - watching {path} and filtering on {file}");

            // Begin watching.
            watcher.EnableRaisingEvents = true;

            //keep thread alive forever
            Application.Run();
        }

        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // filewatcher throws multiple events, we only need 1
            var lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime > _lastRead.AddSeconds(1))
            {
                _lastRead = lastWriteTime;
                Log.Trace("File: " + e.FullPath + " " + e.ChangeType);
                
                try
                {
                    string fileContents;
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
                    Log.Error(exception);
                }
            }
        }
    }
}