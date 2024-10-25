// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using ghosts.client.linux.Comms;
using ghosts.client.linux.Comms.ClientSocket;
using ghosts.client.linux.Infrastructure;
using ghosts.client.linux.timelineManager;
using Ghosts.Domain.Code;
using Ghosts.Domain.Models;
using NLog;

namespace ghosts.client.linux
{
    internal static class Program
    {
        internal static ClientConfiguration Configuration { get; private set; }
        internal static ApplicationDetails.ConfigurationUrls ConfigurationUrls { get; set; }
        internal static bool IsDebug;
        internal static List<ThreadJob> ThreadJobs { get; private set; }
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        public static CheckId CheckId { get; set; }
        internal static BackgroundTaskQueue Queue;

        private static void Main(string[] args)
        {
            ThreadJobs = new List<ThreadJob>();
            ClientConfigurationLoader.UpdateConfigurationWithEnvVars();

            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fatal exception in {ApplicationDetails.Name} {ApplicationDetails.Version}: {e}", Color.Red);
                _log.Fatal(e);
                Console.ReadLine();
            }
        }

        private static void Run(IEnumerable<string> args)
        {
            // ignore all certs
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

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

                async void Start()
                {
                    await c.Run();
                }

                var connectionThread = new Thread(Start) { IsBackground = true };
                connectionThread.Start();
                Queue = c.Queue;
            }

            Program.CheckId = new CheckId();

            //linux clients do not catch stray processes or check for job duplication

            StartupTasks.SetStartup();

            ListenerManager.Run();

            //do we have client id? or is this first run?
            _log.Trace($"CheckID: {Program.CheckId.Id}");

            //connect to command server for updates and sending logs
            Updates.Run();

            //local survey gathers information such as drives, accounts, logs, etc.
            if (Configuration.Survey.IsEnabled)
            {
                _log.Trace("Survey enabled, initalizing...");
                try
                {
                    Survey.SurveyManager.Run();
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
                _log.Trace("Health checks enabled, initalizing...");
                var h = new Health.Check();
                h.Run();
            }
            else
            {
                _log.Trace("Health checks disabled, continuing.");
            }

            if (Configuration.HandlersIsEnabled)
            {
                _log.Trace("Handlers enabled, initalizing...");
                var o = new Orchestrator();
                o.Run();
            }
            else
            {
                _log.Trace("Handling disabed, continuing.");
            }

            new ManualResetEvent(false).WaitOne();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _log.Debug($"Initiating {ApplicationDetails.Name} shutdown - Local: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");
        }
    }
}
