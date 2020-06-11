// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ghosts.client.linux.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;

namespace ghosts.client.linux.Health
{
    public class Check
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly Logger _healthLog = LogManager.GetLogger("HEALTH");

        private static DateTime _lastRead = DateTime.MinValue;
        private List<Thread> _threads { get; set; }

        public Check()
        {
            this._threads = new List<Thread>();
        }

        public void Run()
        {
            try
            {
                // now watch that file for changes
                var watcher = new FileSystemWatcher(ApplicationDetails.ConfigurationFiles.Path);
                watcher.Filter = Path.GetFileName(ApplicationDetails.ConfigurationFiles.Health);
                _log.Trace($"watching {watcher.Path}");
                watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size;
                watcher.EnableRaisingEvents = true;
                watcher.Changed += new FileSystemEventHandler(OnChanged);

                Thread t = null;
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    t = Thread.CurrentThread;
                    t.Name = Guid.NewGuid().ToString();
                    this.RunEx();

                }).Start();

                if (t != null)
                {
                    _log.Trace($"HEALTH THREAD: {t.Name}");
                    this._threads.Add(t);
                }
            }
            catch (Exception exc)
            {
                _log.Error(exc);
            }
        }

        public void Shutdown()
        {
            if (this._threads != null)
            {
                foreach (var thread in this._threads)
                {
                    thread.Abort(null);
                }
            }
        }

        private void RunEx()
        {
            var c = new ConfigHealth(ApplicationDetails.ConfigurationFiles.Health);
            var config = c.Load();
            while (true)
            {
                try
                {
                    var r = HealthManager.Check(config);

                    var o = JsonConvert.SerializeObject(r,
                        Formatting.None,
                        new JsonSerializerSettings
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        });

                    _healthLog.Info($"HEALTH|{DateTime.UtcNow}|{o}");


                    Thread.Sleep(config.Sleep);
                }
                catch (Exception e)
                {
                    _log.Debug(e);
                }
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // filewatcher throws two events, we only need 1
            DateTime lastWriteTime = File.GetLastWriteTime(e.FullPath);
            if (lastWriteTime > _lastRead.AddSeconds(1))
            {
                _lastRead = lastWriteTime;
                _log.Trace("File: " + e.FullPath + " " + e.ChangeType);
                _log.Trace($"Reloading {System.Reflection.MethodBase.GetCurrentMethod().DeclaringType}");

                // now terminate existing tasks and rerun
                this.Shutdown();
                StartupTasks.CleanupProcesses();
                this.RunEx();
            }
        }
    }
}
