// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System.Collections.Generic;
using CommandLine;

namespace Ghosts.Client.Universal.Infrastructure
{
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

        [Option("handle", HelpText = "Run a single handler action and exit (e.g. --handle browserfirefox). Does not start the agent.")]
        public string Handle { get; set; }

        [Option("command", HelpText = "The handler command/verb to run with --handle (e.g. browse, random)")]
        public string Command { get; set; }

        [Option("arg", HelpText = "A command argument for --handle. Repeat for multiple args.")]
        public IEnumerable<string> Args { get; set; }

        [Option("handler-arg", HelpText = "A handler option as key=value for --handle. Repeat for multiple.")]
        public IEnumerable<string> HandlerArgs { get; set; }

        [Option("json", Default = false, HelpText = "With --handle, emit the result as JSON to stdout")]
        public bool Json { get; set; }
    }
}
