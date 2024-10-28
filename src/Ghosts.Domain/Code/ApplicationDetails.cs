// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NLog;

namespace Ghosts.Domain.Code
{
    public static class ApplicationDetails
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static string Header =>
@"             ('-. .-.               .-')    .-') _     .-')    
            ( OO )  /              ( OO ). (  OO) )   ( OO ).  
  ,----.    ,--. ,--. .-'),-----. (_)---\_)/     '._ (_)---\_) 
 '  .-./-') |  | |  |( OO'  .-.  '/    _ | |'--...__)/    _ |  
 |  |_( O- )|   .|  |/   |  | |  |\  :` `. '--.  .--'\  :` `.  
 |  | .--, \|       |\_) |  |\|  | '..`''.)   |  |    '..`''.) 
(|  | '. (_/|  .-.  |  \ |  | |  |.-._)   \   |  |   .-._)   \ 
 |  '--'  | |  | |  |   `'  '-'  '\       /   |  |   \       / 
  `------'  `--' `--'     `-----'  `-----'    `--'    `-----'  

";

        /// <summary>
        ///     Returns current GHOSTS exe name
        /// </summary>
        public static string Name => Assembly.GetEntryAssembly()?.GetName().Name;

        /// <summary>
        ///     Returns current GHOSTS exe version
        /// </summary>
        public static string Version => Assembly.GetEntryAssembly()?.GetName().Version.ToString().ToUpper();

        public static string VersionFile
        {
            get
            {
                var fileName = Assembly.GetEntryAssembly()?.Location;
                return fileName != null ? FileVersionInfo.GetVersionInfo(fileName).FileVersion : "";
            }
        }

        /// <summary>
        ///     Returns installed exe path, for commands like c:\exercise\ghosts\ghosts.exe to work properly
        /// </summary>
        public static string InstalledPath
        {
            get
            {
                try
                {
                    return Clean(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.CodeBase));
                }
                catch
                {
                    return Clean(Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location));
                }
            }
        }

        public static string GetPath(string loc)
        {
            return Path.GetFullPath(Path.Combine(InstalledPath, loc));
        }

        public static bool IsLinux()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        }

        // ReSharper disable once InconsistentNaming
        public static bool IsOSX()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        }

        private static string Clean(string x)
        {
            //linux path is file:/users
            //windows path is file:/z:
            //ugh
            var fileFormat = "file:\\";
            if (IsLinux() || IsOSX()) fileFormat = "file:";

            if (x.Contains(fileFormat)) x = x.Substring(x.IndexOf(fileFormat, StringComparison.InvariantCultureIgnoreCase) + fileFormat.Length);

            x = x.Replace(Convert.ToChar(@"\"), Path.DirectorySeparatorChar);
            x = x.Replace(Convert.ToChar(@"/"), Path.DirectorySeparatorChar);

            return x;
        }

        /// <summary>
        ///     Paths to all the client configuration files. Config files are copyable from one instance to another.
        /// </summary>
        public static class ConfigurationFiles
        {
            public static string InstallPath => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}config{System.IO.Path.DirectorySeparatorChar}";

            public static string DefaultNpcImage => Clean(Path.Combine(InstallPath, "photos", "default.png"));
            public static string Application => Clean(InstallPath + "application.json");
            public static string Health => Clean(InstallPath + "health.json");
            public static string Timeline => Clean(InstallPath + "timeline.json");

            public static string DenyList => Clean(InstallPath + "denylist.txt");
            public static string EmailsFooter => Clean(InstallPath + "emails-footer.txt");

            public static string EmailContent(string raw) => Determine(raw, "email-content.csv");
            public static string EmailReply(string raw) => Determine(raw, "email-reply.csv");
            public static string EmailDomain(string raw) => Determine(raw, "emails-domain.json");
            public static string EmailOutside(string raw) => Determine(raw, "emails-outside.json");
            public static string Dictionary(string raw) => Determine(raw, "dictionary.json");
            public static string FileNames(string raw) => Determine(raw, "filenames.txt");

            public static string ChatMessages(string raw) => Determine(raw, "blog-reply.csv");  //lazy, use blog-reply for now
            public static string LastNames(string raw) => Determine(raw, "last_names.txt");
            public static string FirstNames(string raw) => Determine(raw, "first_names.txt");
            public static string EmailTargets(string raw) => Determine(raw, "email_targets.txt");

            //be lazy and use the Blog content as generic post content as default
            public static string GenericPostContent(string raw) => Determine(raw, "blog-content.csv");
            public static string BlogContent(string raw) => Determine(raw, "blog-content.csv");

            public static string BlogReply(string raw) => Determine(raw, "blog-reply.csv");

            private static string Determine(string raw, string defaultValue)
            {
                return !string.IsNullOrEmpty(raw) ? raw : Clean(InstallPath + defaultValue);
            }
        }

        public static class UserAgents
        {
            public static string Path => InstalledPath +
                                         $"{System.IO.Path.DirectorySeparatorChar}config{System.IO.Path.DirectorySeparatorChar}user-agents{System.IO.Path.DirectorySeparatorChar}";
        }

        /// <summary>
        ///     Instance files are PER CLIENT and are kept separate, so that they are NOT accidentally copied from one host to another
        ///     (TL;DR - never copy instance folder)
        /// </summary>
        public static class InstanceFiles
        {
            public static string Path => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}instance{System.IO.Path.DirectorySeparatorChar}";
            public static string Id => Clean(Path + "id.json");
            public static string SurveyResults => Clean(Path + "survey-results.json");
            public static string FilesCreated => Clean(Path + "files-created.log");
            public static string Trackables => Clean(Path + "trackables.json");
        }

        public static class InstanceDirectories
        {
            public static string Path => InstanceFiles.Path;

            public static string TimelineIn =>
                Clean(Path + $"timeline{System.IO.Path.DirectorySeparatorChar}in{System.IO.Path.DirectorySeparatorChar}");

            public static string TimelineOut =>
                Clean(Path + $"timeline{System.IO.Path.DirectorySeparatorChar}out{System.IO.Path.DirectorySeparatorChar}");
        }

        /// <summary>
        ///     Log files contain both normal debug and exception logging,
        ///     but also the client activity logs that are sent to the API server periodically
        /// </summary>
        public static class LogFiles
        {
            public static string Path => InstalledPath + $"{System.IO.Path.DirectorySeparatorChar}logs{System.IO.Path.DirectorySeparatorChar}";

            public static string ClientUpdates => Clean(Path + "clientupdates.log");
        }

        public class ConfigurationUrls
        {
            public ConfigurationUrls(string rootUrl)
            {
                _root = rootUrl;
            }

            private readonly string _root;
            public string Id => $"{_root}/clientid";
            public string Timeline => $"{_root}/clienttimeline";
            public string Results => $"{_root}/clientresults";
            public string Updates => $"{_root}/clientupdates";
            public string Survey => $"{_root}/clientsurvey";
            public string Socket => $"{_root.Replace("/api", "")}/clientHub";
        }
    }
}
