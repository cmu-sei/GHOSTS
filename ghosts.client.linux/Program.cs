// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using CommandLine;
using ghosts.client.linux.Comms;
using ghosts.client.linux.Infrastructure;
using ghosts.client.linux.timelineManager;
using Ghosts.Domain.Code;
using NLog;

namespace ghosts.client.linux
{
    class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        internal static ClientConfiguration Configuration { get; set; }
        internal static Options OptionFlags;
        internal static bool IsDebug;

        /// <summary>
        /// Defines the flags you can send to the client
        /// </summary>
        internal class Options
        {
            [Option('d', "debug", Default = false, HelpText = "Launch GHOSTS in debug mode")]
            public bool Debug { get; set; }

            [Option('h', "help", Default = false, HelpText = "Display this help screen")]
            public bool Help { get; set; }

            [Option('r', "randomize", Default = false, HelpText = "Create a randomized timeline")]
            public bool Randomize { get; set; }

            [Option('v', "version", Default = false, HelpText = "GHOSTS client version")]
            public bool Version { get; set; }

            [Option('i', "information", Default = false, HelpText = "GHOSTS client id information")]
            public bool Information { get; set; }
        }

        static void Main(string[] args)
        {
            try
            {
                ClientConfigurationLoader.UpdateConfigurationWithEnvVars();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fatal exception in GHOSTS {ApplicationDetails.Version}: {e}");
                Console.ReadLine();
                return;
            }
        }

        private static void Run(string[] args)
        {
            // ignore all certs
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            // parse program flags
            if (!CommandLineFlagManager.Parse(args))
                return;

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            _log.Trace($"Initiating Ghosts startup - Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");

            //load configuration
            try
            {
                Program.Configuration = ClientConfigurationLoader.Config;
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

            //catch any stray processes running and avoid duplication of jobs running
            //StartupTasks.CleanupProcesses();

            //make sure ghosts starts when machine starts
            StartupTasks.SetStartup();

            ListenerManager.Run();

            //check id
            _log.Trace(Comms.CheckId.Id);

            ////connect to command server for updates and sending logs
            Comms.Updates.Run();

            //TODO? should these clients do a local survey?
            //if (Configuration.Survey.IsEnabled)
            //{
            //    try
            //    {
            //        Survey.SurveyManager.Run();
            //    }
            //    catch (Exception exc)
            //    {
            //        _log.Error(exc);
            //    }
            //}

            if (Configuration.HealthIsEnabled)
            {
                try
                {
                    var h = new Health.Check();
                    h.Run();
                }
                catch (Exception exc)
                {
                    _log.Error(exc);
                }
            }

            if (Configuration.HandlersIsEnabled)
            {
                try
                {
                    var o = new Orchestrator();
                    o.Run();
                }
                catch (Exception exc)
                {
                    _log.Error(exc);
                }
            }

            new ManualResetEvent(false).WaitOne();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _log.Debug($"Initiating Ghosts shutdown - Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");
            //StartupTasks.CleanupProcesses();
        }
    }
}
