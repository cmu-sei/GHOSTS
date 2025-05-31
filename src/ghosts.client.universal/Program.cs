// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Comms;
using Ghosts.Client.Universal.Comms.ClientSocket;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Client.Universal.timelineManager;
using Ghosts.Client.Universal.TimelineManager;
using Ghosts.Domain.Code;
using Ghosts.Domain.Models;
using NLog;

namespace Ghosts.Client.Universal
{
    internal static class Program
    {
        internal static ClientConfiguration Configuration { get; private set; }
        internal static ApplicationDetails.ConfigurationUrls ConfigurationUrls { get; set; }
        internal static bool IsDebug;
        internal static ConcurrentDictionary<Guid, TaskJob> RunningTasks { get; } = new();
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public static CheckId CheckId { get; set; }
        internal static BackgroundTaskQueue Queue;

        private static async Task Main(string[] args)
        {
            ClientConfigurationLoader.UpdateConfigurationWithEnvVars();

            try
            {
                await Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fatal exception in {ApplicationDetails.Name} {ApplicationDetails.Version}: {e}", Color.Red);
                _log.Fatal(e);
                Console.ReadLine();
            }
        }

        private static async Task Run(IEnumerable<string> args)
        {
            // parse program flags
            if (!CommandLineFlagManager.Parse(args))
            {
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            _log.Trace($"Initiating {ApplicationDetails.Name} startup - Local: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");

            //load configuration
            try
            {
                Configuration = ClientConfigurationLoader.Config;
                ConfigurationUrls = new ApplicationDetails.ConfigurationUrls(Configuration.ApiRootUrl);
            }
            catch (Exception e)
            {
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var o = $"Exec path: {path} - configuration 404: {ApplicationDetails.ConfigurationFiles.Application} - exiting. Exception: {e}";
                _log.Fatal(o);
                Console.WriteLine(o, Color.Red);
                Console.ReadLine();
                return;
            }

            if (Configuration.Sockets.IsEnabled)
            {
                _log.Trace("Sockets enabled. Connecting...");
                var c = new Connection(Configuration.Sockets);
                Queue = c.Queue;

                _ = Task.Run(() => c.Run());
            }

            CheckId = new CheckId();

            //should we catch stray processes or check for job duplication?
            StartupTasks.SetStartup();

            ListenerManager.Run();

            //do we have client id? or is this first run?
            _log.Trace($"CheckID: {Program.CheckId.Id}");

            //connect to command server for updates and sending logs
            Updates.Run();

            //local survey gathers information such as drives, accounts, logs, etc.
            if (Configuration.Survey.IsEnabled)
            {
                _log.Trace("Survey enabled, initializing...");
                try
                {
                    await Survey.SurveyManager.Run();
                }
                catch (Exception exc)
                {
                    _log.Error(exc);
                }
            }
            else
            {
                _log.Trace("Survey disabled, continuing.");
            }

            if (Configuration.HealthIsEnabled)
            {
                _log.Trace("Health checks enabled, initializing...");
                var h = new Health.Check();
                h.Run();
            }
            else
            {
                _log.Trace("Health checks disabled, continuing.");
            }

            if (Configuration.HandlersIsEnabled)
            {
                _log.Trace("Handlers enabled, initializing...");
                var o = new Orchestrator();
                o.Run();
            }
            else
            {
                _log.Trace("Handling disabed, continuing.");
            }

            await Task.Delay(Timeout.Infinite, CancellationToken.None);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _log.Debug($"Initiating {ApplicationDetails.Name} shutdown - Local: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");
        }
    }
}
