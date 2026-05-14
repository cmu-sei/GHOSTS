// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Versioning;
using CommandLine;
using CommandLine.Text;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Infrastructure
{
    internal static class CommandLineFlagManager
    {
        internal static bool Parse(IEnumerable<string> args)
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
                var machine = new ResultMachine();
                GuestInfoVars.Load(machine);

                Console.WriteLine(JsonConvert.SerializeObject(machine, Formatting.Indented));
                return false;
            }
            // end handling flags that result in program exit

#if DEBUG
            Program.IsDebug = true;
#endif

            if (options.Debug || Program.IsDebug)
            {
                Program.IsDebug = true;
                DebugManager.Run();
            }
            else
            {
                Console.WriteLine($"GHOSTS ({ApplicationDetails.Name}:{ApplicationDetails.Version} [{ApplicationDetails.VersionFile}]) running in production mode. Installed path: {ApplicationDetails.InstalledPath}");
            }

            if (options.Randomize)
            {
                throw new NotImplementedException("Randomize not released yet...");
                //Console.WriteLine("randomize!");
                //return;
            }

            return true;
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
            Console.WriteLine($"{ApplicationDetails.Name}: {ApplicationDetails.Version} [{ApplicationDetails.VersionFile}]");
            foreach (var assemblyName in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
            {
                Console.WriteLine($"{assemblyName.Name}: {assemblyName.Version}");
            }
            Console.WriteLine($"Running on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            Console.WriteLine($"Compiled with: {Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName}");
        }
    }
}
