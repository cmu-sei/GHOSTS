// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using CommandLine;
using CommandLine.Text;
using Ghosts.Domain.Code;
using System;
using System.Reflection;
using Ghosts.Domain;
using Newtonsoft.Json;

namespace Ghosts.Client.Infrastructure;

internal static class CommandLineFlagManager
{
    internal static bool Parse(string[] args)
    {
        Console.WriteLine(ApplicationDetails.Header);
        var options = new Program.Options();
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
            .ParseArguments<Program.Options>(args)
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
        }

        return true;
    }

    private static void Help(ParserResult<Program.Options> parserResults)
    {
        Console.WriteLine($"Hello, and welcome to {ApplicationDetails.Name.ToUpper()}...");
        Console.WriteLine($"The {ApplicationDetails.Name.ToUpper()} client replicates highly-complex, realistic non-player characters (NPCs) on the desktop.");
        Console.WriteLine("Valid options are:");
        Console.WriteLine(
            HelpText.AutoBuild(parserResults, null, null).ToString()
                .Replace("--help             Display this help screen.\r", "")
                .Replace("--version          Display version information.\r", "")
                .Replace("\r\n\r\n\r\n", "")
        );
    }

    private static void Version()
    {
        //handle version flag and return ghosts and referenced assemblies information
        Console.WriteLine($"{ApplicationDetails.Name}:{ApplicationDetails.Version} [{ApplicationDetails.VersionFile}]");
        foreach (var assemblyName in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
        {
            Console.WriteLine($"{assemblyName.Name}: {assemblyName.Version}");
        }
    }
}