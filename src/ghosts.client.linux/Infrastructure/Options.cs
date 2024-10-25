// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using CommandLine;

namespace ghosts.client.linux.Infrastructure
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
    }
}
