// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NLog;

namespace Ghosts.Domain.Code
{
    /// <summary>
    /// This class generates a random user agent from obtainable list files of common user agents 
    /// for all major browsers and "bad" UA strings as well
    /// </summary>
    public static class UserAgentManager
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public static string Get()
        {
            var files = GetFiles();
            var file = files.PickRandom();
            var entries = GetEntries(file.FullName);

            return entries.PickRandom();
        }

        private static FileInfo[] GetFiles()
        {
            var files = new List<FileInfo>();
            try
            {
                var dirPath = ApplicationDetails.InstalledPath + $"{Path.DirectorySeparatorChar}App_Data{Path.DirectorySeparatorChar}user-agents";
                dirPath = dirPath.Replace("file:/", "/");
                foreach (var file in new DirectoryInfo(dirPath).EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly))
                {
                    files.Add(file);
                }
            }
            catch (Exception exc)
            {
                _log.Error(exc);
            }

            return files.ToArray();
        }

        private static string[] GetEntries(string filePath)
        {
            var raw = File.ReadAllText(filePath);
            var textLines = Regex.Split(raw, "\r\n|\r|\n");
            return textLines;
        }
    }
}
