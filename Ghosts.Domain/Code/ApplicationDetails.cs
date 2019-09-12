// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Reflection;
using NLog;
using NLog.Fluent;
using NPOI.OpenXmlFormats.Dml.Chart;

namespace Ghosts.Domain.Code
{
    public static class ApplicationDetails
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        
        /// <summary>
        /// Returns current GHOSTS exe name
        /// </summary>
        public static string Name => Assembly.GetEntryAssembly().GetName().Name;
        /// <summary>
        /// Returns current GHOSTS exe version
        /// </summary>
        public static string Version => Assembly.GetEntryAssembly().GetName().Version.ToString(); 

        /// <summary>
        /// Returns installed exe path, for commands like c:\exercise\ghosts\ghosts.exe to work properly
        /// </summary>
        public static string InstalledPath
        {
            get
            {
                try
                {
                    var x = Clean(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().CodeBase));
                    _log.Trace(x);
                    return x;
                }
                catch
                {
                    return Assembly.GetEntryAssembly().Location;
                }
            }
        }

        /// <summary>
        /// Paths to all the client configuration files. Config files are copyable from one instance to another.
        /// </summary>
        public static class ConfigurationFiles
        {
            //public static string Path => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}config{System.IO.Path.DirectorySeparatorChar}";
            
            public static string Path
            {
                get
                {
                    var x = InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}config{System.IO.Path.DirectorySeparatorChar}";
                    _log.Trace(x);
                    return x;
                }
            }
            
            public static string Application => Clean(Path + "application.json");
            public static string Health => Clean(Path + "health.json");
            public static string Timeline => Clean(Path + "timeline.json");
            public static string EmailContent => Clean(Path + "email-content.csv");
            public static string EmailReply => Clean(Path + "email-reply.csv");
            public static string EmailsDomain => Clean(Path + "emails-domain.json");
            public static string EmailsOutside => Clean(Path + "emails-outside.json");
            public static string Dictionary => Clean(Path + "dictionary.json");
            public static string FileNames => Clean(Path + "filenames.txt");
        }

        public static class UserAgents
        {
            public static string Path => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}config{System.IO.Path.DirectorySeparatorChar}user-agents{System.IO.Path.DirectorySeparatorChar}";
        }
   
        private static string Clean(string x)
        {
            if (x.Contains("file:"))
                x = x.Substring(x.IndexOf("file:", StringComparison.InvariantCultureIgnoreCase) + "file:".Length);

            x = x.Replace(Convert.ToChar(@"\"), System.IO.Path.DirectorySeparatorChar);
            x = x.Replace(Convert.ToChar(@"/"), System.IO.Path.DirectorySeparatorChar);

            return x;
        }

        /// <summary>
        /// Instance files are PER CLIENT and are kept separate, so that they are NOT accidentally copied from one host to another
        /// (TL;DR - never copy instance folder)
        /// </summary>
        public static class InstanceFiles
        {
            public static string Path => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}instance{System.IO.Path.DirectorySeparatorChar}";
            public static string Id => Clean(Path + "id.json");
            public static string SurveyResults => Clean(Path + "survey-results.json");
            public static string FilesCreated => Clean(Path + "files-created.log");
        }

        public static class InstanceDirectories
        {
            public static string Path => InstanceFiles.Path;
            public static string TimelineIn => Clean(Path + $"timeline{System.IO.Path.DirectorySeparatorChar}in{System.IO.Path.DirectorySeparatorChar}");
            public static string TimelineOut => Clean(Path + $"timeline{System.IO.Path.DirectorySeparatorChar}out{System.IO.Path.DirectorySeparatorChar}");
        }

        /// <summary>
        /// Log files contain both normal debug and exception logging, 
        /// but also the client activity logs that are sent to the API server periodically
        /// </summary>
        public static class LogFiles
        {
            public static string Path => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}logs{System.IO.Path.DirectorySeparatorChar}";

            public static string ClientUpdates => Clean(Path + "clientupdates.log");
        }
    }
}
