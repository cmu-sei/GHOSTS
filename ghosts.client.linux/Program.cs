// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using ghosts.client.linux.timelineManager;
using Ghosts.Client.Code;
using Ghosts.Domain.Code;
using NLog;

namespace ghosts.client.linux
{
    class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static bool IsDebug = false;
        internal static ClientConfiguration Configuration { get; set; }

        static void Main(string[] args)
        {
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
            if (args.ToList().Contains("--version"))
            {
                Console.WriteLine(ApplicationDetails.Version);
                return;
            }

#if DEBUG
            IsDebug = true;
#endif

            if (args.ToList().Contains("--debug"))
            {
                Program.IsDebug = true;

                Console.WriteLine($"GHOSTS running in debug mode. Installed path: {ApplicationDetails.InstalledPath}");

                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Application} == {File.Exists(ApplicationDetails.ConfigurationFiles.Application)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Dictionary} == {File.Exists(ApplicationDetails.ConfigurationFiles.Dictionary)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.EmailContent} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailContent)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.EmailReply} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailReply)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.EmailsDomain} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailsDomain)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.EmailsOutside} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailsOutside)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Health} == {File.Exists(ApplicationDetails.ConfigurationFiles.Health)}");
                Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Timeline} == {File.Exists(ApplicationDetails.ConfigurationFiles.Timeline)}");


                Console.WriteLine($"{ApplicationDetails.InstanceFiles.Id} == {File.Exists(ApplicationDetails.InstanceFiles.Id)}");
                Console.WriteLine($"{ApplicationDetails.InstanceFiles.FilesCreated} == {File.Exists(ApplicationDetails.InstanceFiles.FilesCreated)}");
                Console.WriteLine($"{ApplicationDetails.InstanceFiles.SurveyResults} == {File.Exists(ApplicationDetails.InstanceFiles.SurveyResults)}");

                Console.WriteLine($"{ApplicationDetails.LogFiles.ClientUpdates} == {File.Exists(ApplicationDetails.LogFiles.ClientUpdates)}");
            }
            else
            {
                Console.WriteLine($"GHOSTS running in production mode. Installed path: {ApplicationDetails.InstalledPath}");
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);

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
                Console.WriteLine(o);
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
