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

            // Suppress the banner when emitting machine-readable JSON so stdout carries only the result.
            var quiet = !string.IsNullOrEmpty(options.Handle) && options.Json;
            if (!quiet)
                Console.WriteLine(ApplicationDetails.Header);

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

            if (!string.IsNullOrEmpty(options.Handle))
            {
                Environment.ExitCode = HandleCommand.Run(options);
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

            Console.WriteLine("Single-handler mode (run one action and exit, without starting the agent):");
            Console.WriteLine();
            Console.WriteLine("  Usage:");
            Console.WriteLine("    ghosts --handle <handler> --command <verb> [--arg <value> ...] [--handler-arg <key=value> ...] [--json]");
            Console.WriteLine();
            Console.WriteLine("  --command becomes the event Command; each --arg is appended to CommandArgs;");
            Console.WriteLine("  each --handler-arg is added to HandlerArgs. The action runs once (no loop) and");
            Console.WriteLine("  ignores working hours. Exit code is 0 on success, 1 on failure (unknown handler");
            Console.WriteLine("  or the handler threw). With --json, stdout carries only the JSON result and all");
            Console.WriteLine("  logging goes to stderr.");
            Console.WriteLine();
            Console.WriteLine("  IMPORTANT: any value beginning with '-' must be attached with '=', otherwise it");
            Console.WriteLine("  is parsed as another flag. Example: --command=\"-sI https://example.com\"");
            Console.WriteLine();
            Console.WriteLine("  Examples:");
            Console.WriteLine("    ghosts --handle bash --command \"whoami\" --json");
            Console.WriteLine("    ghosts --handle curl --command=\"-sI https://example.com\" --json");
            Console.WriteLine("    ghosts --handle browserfirefox --command browse --arg \"https://example.com\"");
            Console.WriteLine();
            Console.WriteLine("  On non-Windows, Word/Excel/PowerPoint automatically run their cross-platform");
            Console.WriteLine("  Light variants; browser handlers default to headless (override with");
            Console.WriteLine("  --handler-arg isheadless=false).");
            Console.WriteLine();
            Console.WriteLine($"  Valid handlers: {string.Join(", ", Enum.GetNames(typeof(HandlerType)))}");
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
