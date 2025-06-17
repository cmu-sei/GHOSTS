// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghosts.Domain.Code;
using NLog;
using Exception = System.Exception;
using System.Threading;
using Ghosts.Domain;
using Ghosts.Domain.Code.Helpers;

namespace Ghosts.Client.Infrastructure;

/// <summary>
/// Lists and deletes files that were created by ghosts client, so as to avoid high disk usage
/// </summary>
public static class FileListing
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly string _fileName = ApplicationDetails.InstanceFiles.FilesCreated;
    private static readonly object _locked = new object();
    private static readonly object _safetyLocked = new object();
    private static readonly int _sleepTime = 10000;

    public static void Add(string path, HandlerType handlerType)
    {
        try
        {
            if (!File.Exists(_fileName))
                File.Create(_fileName);

            if (!Monitor.IsEntered(_safetyLocked)) //checking if safety net is currently flushing cache
            {
                lock (_locked) //if a thread has entered, the others will wait
                {
                    while (new FileInfo(_fileName).IsFileLocked())
                    {
                        _log.Trace($"{_fileName} is locked, sleeping for {_sleepTime}...");
                        Thread.Sleep(_sleepTime);
                    }

                    File.AppendAllText(_fileName, $"{DateTime.UtcNow}|{handlerType}|{path}{Environment.NewLine}");
                }
            }
            else //sleep if safety net is being safe
            {
                Thread.Sleep(5000);
            }
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }

    public static int GetFileCount(HandlerType handlerType, int maxAgeInHours)
    {
        if (!File.Exists(_fileName))
        {
            File.Create(_fileName);
            return 0;
        }

        return File.ReadAllLines(_fileName).Where(x => x.Contains($"|{handlerType}|") && !x.EndsWith(".pdf"))
            .Select(line => Convert.ToDateTime(line.Split("|").ToArray()[0]))
            .Count(date => date > (DateTime.UtcNow.AddHours(-maxAgeInHours)));
    }

    public static string GetRandomFile(HandlerType handlerType)
    {
        return File.ReadAllLines(_fileName).Where(x => x.Contains($"|{handlerType}|") && !x.EndsWith(".pdf")).PickRandom().Split("|").ToArray()[2].ToString();
    }

    /// <summary>
    /// Deletes all files in the "ApplicationDetails.InstanceFiles.FilesCreated" cache file
    /// </summary>
    public static void FlushList()
    {
        //check if flushing
        if (Program.Configuration.OfficeDocsMaxAgeInHours == -1)
            return;

        if (!File.Exists(_fileName))
            return;

        //locking thread to make sure files can't write to the log

        _log.Trace("Flushing list...");
        try
        {
            var deletedFiles = new List<string>();

            while (new FileInfo(_fileName).IsFileLocked())
            {
                _log.Trace($"{_fileName} is locked, sleeping for {_sleepTime}...");
                Thread.Sleep(_sleepTime);
            }

            foreach (var line in File.ReadAllLines(_fileName))
            {
                //new style
                var arr = line.Split("|").ToArray();
                if (arr.Count() > 2)
                {
                    var date = Convert.ToDateTime(arr[0]);
                    var handlerType = arr[1];
                    var path = arr[2];
                    if (date < (DateTime.UtcNow.AddHours(-Program.Configuration.OfficeDocsMaxAgeInHours)))
                    {
                        try
                        {
                            if (File.Exists(path))
                            {
                                _log.Trace($"Deleting: {path}");
                                File.Delete(path);
                            }
                            deletedFiles.Add(line);
                        }
                        catch (Exception e)
                        {
                            _log.Warn($"Could not delete file {_fileName}: {e}");
                        }
                    }
                    continue;
                }

                //old style TODO:depreciated
                FileInfo file;
                try
                {
                    file = new FileInfo(line);
                }
                catch (Exception e)
                {
                    _log.Warn($"Could not create FileInfo with file {line}: {e}");
                    deletedFiles.Add(line);
                    continue;
                }

                _log.Trace(
                    $"Delete evaluation for {file.FullName} {file.CreationTime} vs. {DateTime.Now.AddHours(-Program.Configuration.OfficeDocsMaxAgeInHours)}");
                if (!file.Exists || (file.CreationTime <
                                     (DateTime.Now.AddHours(-Program.Configuration.OfficeDocsMaxAgeInHours))))
                    continue;

                try
                {
                    _log.Trace($"Deleting: {file.FullName}");
                    file.Delete();
                    deletedFiles.Add(file.FullName);
                }
                catch (Exception e)
                {
                    _log.Warn($"Could not delete file {_fileName}: {e}");
                }
            }


            if (deletedFiles.Count < 0) return;

            lock (_safetyLocked)
            {
                var recordsToWrite = new List<string>();
                foreach (var line in File.ReadAllLines(_fileName))
                {
                    if (deletedFiles.Any(x => x.Equals(line, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        // was deleted
                    }
                    else
                    {
                        recordsToWrite.Add(line);
                    }
                }

                File.WriteAllLines(_fileName, recordsToWrite);
            }
        }
        catch (Exception e)
        {
            _log.Error($"Error flushing {_fileName}: {e}");
        }
    }
}