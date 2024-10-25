// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Ghosts.Domain.Code.Helpers;
using NLog;

namespace Ghosts.Domain.Code
{
    /// <summary>
    ///     This class generates a random user agent from obtainable list files of common user agents
    ///     for all major browsers and "bad" UA strings as well
    /// </summary>
    public static class UserAgentManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static string Get()
        {
            var files = GetFiles();
            if (!files.Any())
                return string.Empty;
            var file = files.PickRandom();
            var entries = GetEntries(file.FullName);

            return entries.PickRandom();
        }

        public static string GetBrowserSpecific(string type)
        {
            var file = Path.Combine(ApplicationDetails.UserAgents.Path, type, ".txt");
            var entries = GetEntries(file);
            return entries.PickRandom();
        }

        private static FileInfo[] GetFiles()
        {
            var files = new List<FileInfo>();
            try
            {
                _log.Trace("user agents path", ApplicationDetails.UserAgents.Path);
                foreach (var file in new DirectoryInfo(ApplicationDetails.UserAgents.Path).EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly))
                    files.Add(file);
            }
            catch (Exception exc)
            {
                _log.Error(exc);
            }

            return files.ToArray();
        }

        private static IEnumerable<string> GetEntries(string filePath)
        {
            _log.Trace(filePath);
            var raw = File.ReadAllText(filePath);
            var textLines = Regex.Split(raw, "\r\n|\r|\n");
            return textLines;
        }
    }
}
