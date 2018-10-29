// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code;
using NLog;

namespace Ghosts.Client.Code
{
    /// <summary>
    /// Lists and deletes files that were created by ghosts client, so as to avoid high disk usage
    /// </summary>
    public static class FileListing
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private static readonly string _fileName = ApplicationDetails.InstanceFiles.FilesCreated;

        public static void Add(string path)
        {
            try
            {
                if (!File.Exists(_fileName))
                    File.Create(_fileName);

                using (var writetext = new StreamWriter(_fileName, true))
                {
                    writetext.WriteLine(path);
                }
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
        }

        /// <summary>
        /// Deletes all files in the "ApplicationDetails.InstanceFiles.FilesCreated" cache file
        /// </summary>
        public static void FlushList()
        {
            //check if flushing
            if (Program.Configuration.OfficeDocsMaxAgeInHours == -1)
                return;

            _log.Trace("Flushing list...");
            try
            {
                var deletedFiles = new List<string>();

                using (var reader = new StreamReader(_fileName))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var file = new FileInfo(line);
                        _log.Trace($"file is {file.FullName} {file.CreationTime}");
                        if (file.Exists && file.CreationTime < DateTime.UtcNow.AddHours(-Program.Configuration.OfficeDocsMaxAgeInHours)) //clean up and delete files older than x hours
                        {
                            try
                            {
                                _log.Trace($"deleting: {file.FullName}");
                                file.Delete();
                                deletedFiles.Add(file.FullName);
                            }
                            catch (Exception e)
                            {
                                _log.Debug($"Could not delete file {e}");
                            }
                        }
                    }
                }

                if (deletedFiles.Count > 0)
                {
                    var lines = File.ReadAllLines(_fileName).ToList();
                    foreach (var line in lines.ToArray())
                    {
                        if (deletedFiles.Contains(line))
                        {
                            lines.Remove(line);
                        }
                    }
                    File.WriteAllLines(_fileName, lines);
                }
            }
            catch (Exception e)
            {
                _log.Error($"Error flushing list {e}");
            }
        }
    }
}
