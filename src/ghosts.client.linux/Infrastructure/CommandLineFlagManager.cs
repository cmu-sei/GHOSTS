using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using CommandLine;
using CommandLine.Text;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;

namespace ghosts.client.linux.Infrastructure
{
    internal static class CommandLineFlagManager
    {
        internal static bool Parse(string[] args)
        {
            Console.WriteLine(ApplicationDetails.Header);
            
            var options = new Options();
            var parser = new Parser(with =>
            {
                with.EnableDashDash = true;
                with.CaseSensitive = false;
                with.AutoVersion = false;
                with.IgnoreUnknownArguments = true;
                with.AutoHelp = false;
                with.HelpWriter = null;
            });
            var parserResults = parser
                .ParseArguments<Options>(args)
                .WithParsed(o => options = o);

            Program.OptionFlags = options;

            // start handling flags that result in program exit
            if (options.Help)
            {
                Help(parserResults);
                return false;
            }

            if (options.Version)
            {
                Version();
                return false;
            }

            if (options.Information)
            {
                Console.WriteLine(JsonConvert.SerializeObject(new ResultMachine(), Formatting.Indented));
                return false;
            }
            // end handling flags that result in program exit

#if DEBUG
            Program.IsDebug = true;
#endif

            if (options.Debug || Program.IsDebug)
            {
                Program.IsDebug = true;
                Debug();
            }
            else
            {
                Console.WriteLine(
                    $"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version}) running in production mode. Installed path: {ApplicationDetails.InstalledPath}");
            }

            if (options.Randomize)
            {
                throw new NotImplementedException("Randomize not released yet...");
                //Console.WriteLine("randomize!");
                //return;
            }

            return true;
        }

        private static void Debug()
        {
            Console.WriteLine(
                $"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version}) running in debug mode. Installed path: {ApplicationDetails.InstalledPath}");

            Console.WriteLine(
                $"{ApplicationDetails.ConfigurationFiles.Application} == {File.Exists(ApplicationDetails.ConfigurationFiles.Application)}");
            Console.WriteLine(
                $"{ApplicationDetails.ConfigurationFiles.Dictionary} == {File.Exists(ApplicationDetails.ConfigurationFiles.Dictionary)}");
            Console.WriteLine(
                $"{ApplicationDetails.ConfigurationFiles.EmailContent} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailContent)}");
            Console.WriteLine(
                $"{ApplicationDetails.ConfigurationFiles.EmailReply} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailReply)}");
            Console.WriteLine(
                $"{ApplicationDetails.ConfigurationFiles.EmailsDomain} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailsDomain)}");
            Console.WriteLine(
                $"{ApplicationDetails.ConfigurationFiles.EmailsOutside} == {File.Exists(ApplicationDetails.ConfigurationFiles.EmailsOutside)}");
            Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Health} == {File.Exists(ApplicationDetails.ConfigurationFiles.Health)}");
            Console.WriteLine($"{ApplicationDetails.ConfigurationFiles.Timeline} == {File.Exists(ApplicationDetails.ConfigurationFiles.Timeline)}");

            Console.WriteLine($"{ApplicationDetails.InstanceFiles.Id} == {File.Exists(ApplicationDetails.InstanceFiles.Id)}");
            Console.WriteLine($"{ApplicationDetails.InstanceFiles.FilesCreated} == {File.Exists(ApplicationDetails.InstanceFiles.FilesCreated)}");
            Console.WriteLine($"{ApplicationDetails.InstanceFiles.SurveyResults} == {File.Exists(ApplicationDetails.InstanceFiles.SurveyResults)}");

            Console.WriteLine($"{ApplicationDetails.LogFiles.ClientUpdates} == {File.Exists(ApplicationDetails.LogFiles.ClientUpdates)}");
        }

        private static void Help(ParserResult<Options> parserResults)
        {
            Console.WriteLine($"Hello, and welcome to {ApplicationDetails.Name.ToUpper()}...");
            Console.WriteLine(
                $"The {ApplicationDetails.Name.ToUpper()} client replicates highly-complex, realistic non-player characters (NPCs) on the desktop.");
            Console.WriteLine("Valid options are:");
            Console.WriteLine(
                HelpText.AutoBuild(parserResults, null, null).ToString()
                    .Replace("--help             Display this help screen.", "")
                    .Replace("--version          Display version information.", "")
                    .Replace("\n\n\n", "")
            );
        }

        private static void Version()
        {
            //handle version flag and return ghosts and referenced assemblies information
            Console.WriteLine($"{ApplicationDetails.Name}:{ApplicationDetails.Version}");
            foreach (var assemblyName in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                Console.WriteLine($"{assemblyName.Name}: {assemblyName.Version}");
            }
            Console.WriteLine($"Running on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            Console.WriteLine($"Compiled with: {Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName}");
        }
    }
}