// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Ghosts.Domain;

namespace ghosts.client.linux.handlers;

/// <summary>
/// Watcher is file only at the moment
/// </summary>
internal class Watcher : BaseHandler
{
    public Watcher(TimelineHandler handler)
    {
        Init(handler);
        _log.Trace("Spawning watcher handler...");

        try
        {
            Ex(handler);
        }
        catch (ThreadAbortException)
        {
            //ignore
        }
        catch (ThreadInterruptedException)
        {
            //ignore
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
    }


    //
    // This is changed from Windows versions as we don't have the Application.Run()
    // method available which is in the Forms library.
    //
    public static void Ex(TimelineHandler handler)
    {

        List<FolderWatcher> folderWatcherList = new List<FolderWatcher>();
        List<FileWatcher> fileWatcherList = new List<FileWatcher>();
        try
        {



            //first, create all of the watchers
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                //TODO Need to port working hours/process stuff
                //Infrastructure.WorkingHours.Is(handler);


                _log.Trace($"Watcher: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    //watch a folder
                    case "folder":
                        var fw = new FolderWatcher(handler, timelineEvent, timelineEvent.Command);
                        folderWatcherList.Add(fw);
                        break;
                    //File
                    default:
                        var w = new FileWatcher(handler, timelineEvent, timelineEvent.Command);
                        fileWatcherList.Add(w);
                        break;
                }


            }
            //Now we have to loop forever
            while (true)
            {
                Thread.Sleep(2000); //default
            }
        }
        catch (Exception)
        {
            throw;  //will be logged at next level
        }
        finally
        {
            foreach (var w in folderWatcherList)
            {
                try
                {
                    w.Dispose();
                }
                catch { }
            }
            foreach (var w in fileWatcherList)
            {
                try
                {
                    w.Dispose();
                }
                catch { }
            }
        }
    }
}


/// <summary>
/// Helper class to sort file list by size or creation time.
/// </summary>
internal class Afile
{
    public string name { get; set; }
    public long size { get; set; }
    public DateTime ctime { get; set; }

    //default sort method uses only the size, returns largest to smallest
    public static int CompareBySize(Afile x, Afile y)
    {
        // A null value means that this object is greater.
        if (y.size > x.size)
            return 1;
        else if (x.size == y.size) return 0;
        else return -1;
    }

    //this sort returns oldest to newest
    public static int CompareByDate(Afile x, Afile y)
    {
        // A null value means that this object is greater.
        if (x.ctime > y.ctime)
            return 1;
        else if (x.ctime == y.ctime) return 0;
        else return -1;
    }



}

internal class FolderWatcher : BaseHandler
{
    private readonly TimelineHandler _handler;
    private readonly TimelineEvent _timelineEvent;
    private readonly string _command;
    private readonly string folderPath = null;
    private readonly long folderMaxSize = -1;   //in bytes
    private long lastFolderSize = -1;  // last known size of this folder
    private readonly string deletionApproach = "random";
    private readonly FileSystemWatcher watcher = null;

    public void Dispose()
    {
        watcher?.Dispose();
    }

    /// <summary>
    /// Return all files (recursively) under starting folder
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="allfiles"></param>
    static void GetAllFiles(string folder, List<Afile> allfiles)
    {
        // Get array of all file names.
        var filelist = Directory.GetFiles(folder, "*");
        foreach (var fname in filelist)
        {
            FileInfo info = new FileInfo(fname);
            allfiles.Add(new Afile() { name = info.FullName, size = info.Length, ctime = info.CreationTime });
        }
        //get subdirectories
        var dirlist = Directory.GetDirectories(folder, "*");
        foreach (var dname in dirlist)
        {
            GetAllFiles(dname, allfiles);
        }
    }

    /// <summary>
    /// Return total folder size including all sub folders (recursively)
    /// </summary>
    /// <param name="folder"></param>
    /// <returns></returns>
    static long GetDirectorySize(string folder)
    {
        // Get array of all file names.
        var filelist = Directory.GetFiles(folder, "*");

        // Sum file sizes
        long size = 0;
        foreach (var fname in filelist)
        {
            try
            {
                FileInfo info = new FileInfo(fname);
                size += info.Length;
            }
            catch { }  //ignore any errors when accessing the file
        }
        //recurse, sum sub directory sizes
        var dirlist = Directory.GetDirectories(folder, "*");
        foreach (var dname in dirlist)
        {
            size += GetDirectorySize(dname);
        }
        // Return total size
        return size;
    }

    internal FolderWatcher(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        _handler = handler;
        _timelineEvent = timelineEvent;
        _command = command;



        //parse the command args
        var charSeparators = new char[] { ':' };
        foreach (var cmd in timelineEvent.CommandArgs)
        {
            //each argument string is key:value, parse this
            var argString = cmd.ToString();
            if (!string.IsNullOrEmpty(argString))
            {
                var words = argString.Split(charSeparators, 2, StringSplitOptions.None);
                if (words.Length == 2)
                {
                    if (words[0] == "path") folderPath = Environment.ExpandEnvironmentVariables(words[1]);
                    else if (words[0] == "size" && long.TryParse(words[1], out var localsize)) folderMaxSize = localsize * 1024 * 1024;
                    else if (words[0] == "deletionApproach") deletionApproach = words[1];
                }
            }
        }

        //validate the arguments
        if (folderPath == null)
        {
            _log.Trace("In Watcher handler, no 'path' argument specified for a folder path, Watcher exiting.");
            return;
        }
        if (!Directory.Exists(folderPath))
        {
            _log.Trace("In Watcher handler, 'path' argument " + folderPath + " is not a valid folder, Watcher exiting.");
            return;
        }

        if (folderMaxSize == -1)
        {
            _log.Trace("In Watcher handler, no 'size' argument is specified for a folder path, Watcher exiting.");
            return;
        }

        if (!(deletionApproach == "random" || deletionApproach == "oldest" || deletionApproach == "largest"))
        {
            _log.Trace("In Watcher handler, 'deletionApproach' argument {deletionApproach} is unrecognized using random.");
            deletionApproach = "random";
        }

        lastFolderSize = GetDirectorySize(folderPath);

        watcher = new FileSystemWatcher(folderPath)
        {
            NotifyFilter = NotifyFilters.Size,
            EnableRaisingEvents = true,
            IncludeSubdirectories = true
        };
        watcher.Changed += OnChanged;

        _log.Trace($"Setting up watcher for folder {folderPath} with maxSize {folderMaxSize} (total bytes)");

        // Begin watching.
        watcher.EnableRaisingEvents = true;

        //keep thread alive forever
        //Application.Run();
    }

    /// <summary>
    /// Triggered only folder size change
    /// Will do nothing if folder size shrinks; action is taken if folder size greater than current size
    /// Approach is to get a file list, then loop deleting by either random, oldest, or largest
    /// until target size is reached.
    /// </summary>
    /// <param name="source"></param>
    /// <param name="e"></param>
    private void OnChanged(object source, FileSystemEventArgs e)
    {
        try
        {
            // ignore if size is shrinking
            long currentSize;
            try
            {
                currentSize = GetDirectorySize(folderPath);
            }
            catch
            {
                return; //if  there was an exception, return and try again next time.
            }
            if (currentSize > lastFolderSize && currentSize > folderMaxSize)
            {
                //size has grown, exceeds max, need to delete
                //get a list of all files with their sizes
                List<Afile> allfiles = new List<Afile>();
                try
                {
                    GetAllFiles(folderPath, allfiles);
                }
                catch
                {
                    return; //if  there was an exception, return and try again next time.
                }
                //do the sort here before looping and deleting
                if (deletionApproach == "oldest")
                {
                    allfiles.Sort(Afile.CompareByDate);
                }
                else if (deletionApproach == "largest")
                {
                    allfiles.Sort(Afile.CompareBySize);
                }

                while (true)
                {
                    Afile targetFile = null;

                    if (deletionApproach == "oldest" || deletionApproach == "largest")
                    {
                        targetFile = allfiles[0];
                        allfiles.RemoveAt(0);
                    }
                    else
                    {
                        //default is random
                        var targetIndex = _random.Next(0, allfiles.Count);
                        targetFile = allfiles[targetIndex];
                        allfiles.RemoveAt(targetIndex);
                    }
                    //now delete the file
                    try
                    {
                        File.Delete(targetFile.name);
                        //update current size
                        currentSize -= targetFile.size;
                        _log.Trace($"Watcher: successfully deleted {targetFile.name} to reduce folder {folderPath} size.");
                        if (currentSize < folderMaxSize)
                        {
                            _log.Trace($"Watcher: successfully reduced folder {folderPath} size to target {folderMaxSize} (bytes).");
                            break;  //break from loop, we have hit the target size
                        }

                    }
                    catch
                    {
                        //ignore the exception, file may be protected or in the process of being written
                        _log.Trace($"Watcher: unable to delete {targetFile.name} to reduce folder {folderPath} size, either in use or protected.");
                    }
                    if (allfiles.Count == 0)
                    {
                        //no more files to try to delete and loop is still running. 
                        break; //break out of the loop

                    }

                }

                lastFolderSize = currentSize;
            }
            else
            {
                lastFolderSize = currentSize;
            }
        }
        catch (Exception exception)
        {
            _log.Trace("Watcher:: Unexpected exception.");
            _log.Error(exception);
        }
    }

}

internal class FileWatcher : BaseHandler
{
    private readonly TimelineHandler _handler;
    private readonly TimelineEvent _timelineEvent;
    private readonly string _command;
    private readonly string _filePath;
    private static DateTime _lastRead = DateTime.MinValue;

    private readonly FileSystemWatcher watcher = null;

    public void Dispose()
    {
        watcher?.Dispose();
    }


    internal FileWatcher(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        _handler = handler;
        _timelineEvent = timelineEvent;
        _command = command;

        _filePath = timelineEvent.CommandArgs[0].ToString();
        var sleepTime = Convert.ToInt32(timelineEvent.CommandArgs[1]);

        //if (timelineEvent.CommandArgs[2] != null)
        //{
        //    var webhookPayload = timelineEvent.CommandArgs[2].ToString();
        //    this.WebhookCreate(webhookPayload);
        //}

        if (string.IsNullOrEmpty(_filePath))
        {
            _log.Trace("file path null or empty");
            return;
        }

        var path = string.Empty;
        var file = string.Empty;

        var attr = File.GetAttributes(_filePath);
        if (attr.HasFlag(FileAttributes.Directory))
        {
            path = _filePath;
            _log.Trace($"Directory passed: {path}");
        }
        else
        {
            //MessageBox.Show("Its a file");
            var f = new FileInfo(_filePath);
            path = f.DirectoryName;
            file = f.Name;
            _log.Trace($"File passed - Directory : {path} File: {file}");
        }

        watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };

        if (!string.IsNullOrEmpty(file))
            watcher.Filter = file;

        watcher.EnableRaisingEvents = true;
        watcher.Changed += OnChanged;

        _log.Trace($"Setting up watcher for {_filePath} - watching {path} and filtering on {file}");

        // Begin watching.
        watcher.EnableRaisingEvents = true;

        //keep thread alive forever - this is not available in Net 6.0
        //Application.Run();
    }

    // Define the event handlers.
    private void OnChanged(object source, FileSystemEventArgs e)
    {
        // filewatcher throws multiple events, we only need 1
        var lastWriteTime = File.GetLastWriteTime(e.FullPath);
        if (lastWriteTime > _lastRead.AddSeconds(1))
        {
            _lastRead = lastWriteTime;
            _log.Trace("File: " + e.FullPath + " " + e.ChangeType);

            try
            {
                string fileContents;
                using (var logFileStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var logFileReader = new StreamReader(logFileStream))
                    {
                        fileContents = logFileReader.ReadToEnd();
                    }
                }

                //this.Report(_handler.HandlerType.ToString(), _command, _filePath, _timelineEvent.TrackableId, fileContents);

                if (Program.IsDebug)
                    Console.WriteLine($"File: {e.FullPath} : {e.ChangeType} : {fileContents}");
            }
            catch (Exception exception)
            {
                _log.Error(exception);
            }
        }
    }
}
