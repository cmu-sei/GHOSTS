// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;
using Ghosts.Client.Infrastructure;
using Ghosts.Client.TimelineManager;
using Ghosts.Domain.Code;
using Ghosts.Domain.Models;
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

        internal static List<ThreadJob> ThreadJobs { get; set; }
        internal static ClientConfiguration Configuration { get; set; }
        internal static Options OptionFlags;
        internal static bool IsDebug;

        // minimize memory use
        [DllImport("psapi.dll")]
        static extern int EmptyWorkingSet(IntPtr hwProc);

        static void MinimizeFootprint()
        {
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }

        private static void minimizeMemory()
        {
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle,
                (UIntPtr)0xFFFFFFFF, (UIntPtr)0xFFFFFFFF);
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process,
            UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);
        // end minimize memory use

        internal class Options
        {
            [Option('d', "debug", Default = false, HelpText = "Launch in debug mode")]
            public bool Debug { get; set; }

            [Option('h', "help", Default = false, HelpText = "Display this help screen")]
            public bool Help { get; set; }

            [Option('v', "version", Default = false, HelpText = "Client version")]
            public bool Version { get; set; }

            [Option('i', "information", Default = false, HelpText = "Client id information")]
            public bool Information { get; set; }
        }

        [STAThread]
        static void Main(string[] args)
        {
            MinimizeFootprint();
            minimizeMemory();
            
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                var s = $"Fatal exception in {ApplicationDetails.Name} {ApplicationDetails.Version}: {e}";
                _log.Fatal(s);

                var handle = GetConsoleWindow();
                ShowWindow(handle, SwShow);

                Console.WriteLine(s);
                Console.ReadLine();
            }
        }

        private static void Run(string[] args)
        {
            // ignore all certs
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            // parse program flags
            if (!CommandLineFlagManager.Parse(args))
                return;
            
            //attach handler for shutdown tasks
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            _log.Trace($"Initiating {ApplicationDetails.Name} startup - Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");

            ThreadJobs = new List<ThreadJob>();

            //load configuration
            try
            {
                Configuration = ClientConfigurationLoader.Config;
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

            DebugManager.Evaluate();

            StartupTasks.CheckConfigs();

            Thread.Sleep(500);
            
            //show window if debugging or if --debug flag passed in
            var handle = GetConsoleWindow();
            if (!IsDebug)
            {
                ShowWindow(handle, SwHide);
                //add hook to manage processes running in order to never tip a machine over
                StartupTasks.CleanupProcesses();
            }

            if (!Configuration.DisableStartup)
            {
                //add ghosts to startup
                StartupTasks.SetStartup();
            }

            //add listener on a port or ephemeral file watch to handle ad hoc commands
            ListenerManager.Run();

            //do we have client id? or is this first run?
            _log.Trace($"CheckID: {Comms.CheckId.Id}");

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
                    _log.Error($"Exception instantiating survey: {exc}");
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
                    _log.Error($"Exception instantiating health: {exc}");
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
                    _log.Error($"Exception instantiating orchestrator: {exc}");
                }
            }

            //ghosts singleton
            new ManualResetEvent(false).WaitOne();
        }

        //hook for shutdown tasks
        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _log.Debug($"Initiating {ApplicationDetails.Name} shutdown - Local time: {DateTime.Now.TimeOfDay} UTC: {DateTime.UtcNow.TimeOfDay}");
            StartupTasks.CleanupProcesses();
        }
    }
}