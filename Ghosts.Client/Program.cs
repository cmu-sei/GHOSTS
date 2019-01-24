// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Ghosts.Client.Code;
using Ghosts.Client.TimelineManager;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client
{
    class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SwHide = 0;
        private const int SwShow = 5;

        internal static ClientConfiguration Configuration { get; set; }
        internal static bool IsDebug;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                var s = $"Fatal exception in GHOSTS {ApplicationDetails.Version}: {e}";
                _log.Fatal(s);

                var handle = GetConsoleWindow();
                ShowWindow(handle, SwShow);

                Console.WriteLine(s);
                Console.ReadLine();
            }
        }

        private static void Run(string[] args)
        {
            //handle flags
            if (args.ToList().Contains("--version"))
            {
                //handle version flag and return ghosts and referenced assemblies information
                Console.WriteLine($"{ApplicationDetails.Name}:{ApplicationDetails.Version}");
                foreach (var assemblyName in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
                {
                    Console.WriteLine($"{assemblyName.Name}: {assemblyName.Version}");
                }
                return;
            }

#if DEBUG
            Program.IsDebug = true;        
#endif

            if (args.ToList().Contains("--debug") || Program.IsDebug)
            {
                Program.IsDebug = true;
                
                Console.WriteLine($"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version}) running in debug mode. Installed path: {ApplicationDetails.InstalledPath}");

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
                Console.WriteLine($"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version}) running in production mode. Installed path: {ApplicationDetails.InstalledPath}");
            }
            
            //attach handler for shutdown tasks
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

            StartupTasks.CheckConfigs();

            Thread.Sleep(500);
            //show window if debugging or if --debug flag passed in
            var handle = GetConsoleWindow();
            if (!Program.IsDebug)
            {
                ShowWindow(handle, SwHide);
                //add hook to manage processes running in order to never tip a machine over
                StartupTasks.CleanupProcesses();
            }

            //add ghosts to startup
            StartupTasks.SetStartup();

            //add listener on a port or ephemeral file watch to handle ad hoc commands
            ListenerManager.Run();

            //do we have client id? or is this first run?
            _log.Trace(Comms.CheckId.Id);

            //connect to command server for 1) client id 2) get updates and 3) sending logs/surveys
            Comms.Updates.Run();

            //local survey gathers information such as drives, accounts, logs, etc.
            if (Configuration.Survey.IsEnabled)
            {
                try
                {
                    Survey.SurveyManager.Run();
                }
                catch (Exception exc)
                {
                    _log.Error(exc);
                }
            }

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

            //timeline processing
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

            //ghosts singleton
            new ManualResetEvent(false).WaitOne();
        }

        //hook for shutdown tasks
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _log.Debug($"Initiating Ghosts shutdown - Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");
            StartupTasks.CleanupProcesses();
        }
    }
}