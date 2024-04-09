// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Handlers;

/// <summary>
/// Watcher is file only at the moment
/// </summary>
internal class Watcher : BaseHandler
{
    public Watcher(TimelineHandler handler)
    {
        base.Init(handler);
        Log.Trace("Spawning watcher handler...");

        Log.Trace("Can't loop on watcher!...");

        try
        {
            Ex(handler);
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
    }

    public void Ex(TimelineHandler handler)
    {
        foreach (TimelineEvent timelineEvent in handler.TimeLineEvents)
        {
            WorkingHours.Is(handler);

            if (timelineEvent.DelayBeforeActual > 0)
            {
                Thread.Sleep(timelineEvent.DelayBeforeActual);
            }

            Log.Trace($"Watcher: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                //watch a folder
                case "folder":
                    var fw = new FolderWatcher(handler, timelineEvent, timelineEvent.Command);
                    break;
                //File
                default:
                    var w = new FileWatcher(handler, timelineEvent, timelineEvent.Command);
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
            {
                Thread.Sleep(timelineEvent.DelayAfterActual);
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
        if ( y.size > x.size)
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
    private TimelineHandler _handler;
    private TimelineEvent _timelineEvent;
    private readonly string _command;
    private string folderPath = null;
    private long folderMaxSize = -1;   //in bytes
    private long lastFolderSize = -1;  // last known size of this folder
    private string deletionApproach = "random";
    private FileSystemWatcher watcher;

    /// <summary>
    /// Return all files (recursively) under starting folder
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="allfiles"></param>
    static void GetAllFiles(string folder, List<Afile> allfiles )
    {
        // Get array of all file names.
        string[] filelist = Directory.GetFiles(folder, "*");
        foreach (string fname in filelist)
        {
            FileInfo info = new FileInfo(fname);
            allfiles.Add(new Afile() { name = info.FullName, size = info.Length, ctime = info.CreationTime });
        }
        //get subdirectories
        string[] dirlist = Directory.GetDirectories(folder, "*");
        foreach (string dname in dirlist)
        {
            GetAllFiles(dname,allfiles);
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
        string[] filelist = Directory.GetFiles(folder, "*");

        // Sum file sizes
        long size = 0;
        foreach (string fname in filelist)
        {
            try
            {
                FileInfo info = new FileInfo(fname);
                size += info.Length;
            }
            catch { }  //ignore any errors when accessing the file
        }
        //recurse, sum sub directory sizes
        string [] dirlist = Directory.GetDirectories(folder, "*");
        foreach (string dname in dirlist)
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
        char[] charSeparators = new char[] { ':' };
        foreach (var cmd in timelineEvent.CommandArgs)
        {
            //each argument string is key:value, parse this
            var argString = cmd.ToString();
            if (!string.IsNullOrEmpty(argString)) {
                var words = argString.Split(charSeparators, 2, StringSplitOptions.None);
                if (words.Length == 2)
                {
                    if (words[0] == "path") folderPath = Environment.ExpandEnvironmentVariables(words[1]);
                    else if (words[0] == "size" && Int64.TryParse(words[1], out long localsize)) folderMaxSize = localsize * 1024 * 1024;
                    else if (words[0] == "deletionApproach") deletionApproach = words[1];
                }
            }
        }

        //validate the arguments
        if (folderPath == null) {
            Log.Trace("In Watcher handler, no 'path' argument specified for a folder path, Watcher exiting.");
            return;
        }
        if (!Directory.Exists(folderPath))
        {
            Log.Trace("In Watcher handler, 'path' argument " + folderPath + " is not a valid folder, Watcher exiting.");
            return;
        }

        if (folderMaxSize == -1)
        {
            Log.Trace("In Watcher handler, no 'size' argument is specified for a folder path, Watcher exiting.");
            return;
        }

        if (!(deletionApproach == "random" || deletionApproach == "oldest" || deletionApproach == "largest"))
        {
            Log.Trace("In Watcher handler, 'deletionApproach' argument {deletionApproach} is unrecognized using random.");
            deletionApproach = "random";
        }

        lastFolderSize = GetDirectorySize(folderPath);

        watcher = new FileSystemWatcher(folderPath);
        watcher.NotifyFilter = NotifyFilters.Size;
        watcher.EnableRaisingEvents = true;
        watcher.IncludeSubdirectories = true;
        watcher.Changed += OnChanged;

        Log.Trace($"Setting up watcher for folder {folderPath} with maxSize {folderMaxSize} (total bytes)");

        // Begin watching.
        watcher.EnableRaisingEvents = true;

        //keep thread alive forever
        Application.Run();
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
                        int targetIndex = _random.Next(0, allfiles.Count);
                        targetFile = allfiles[targetIndex];
                        allfiles.RemoveAt(targetIndex);
                    }
                    //now delete the file
                    try
                    {
                        File.Delete(targetFile.name);
                        //update current size
                        currentSize = currentSize - targetFile.size;
                        Log.Trace($"Watcher: successfully deleted {targetFile.name} to reduce folder {folderPath} size.");
                        if (currentSize < folderMaxSize)
                        {
                            Log.Trace($"Watcher: successfully reduced folder {folderPath} size to target {folderMaxSize} (bytes).");
                            break;  //break from loop, we have hit the target size
                        }

                    }
                    catch
                    {
                        //ignore the exception, file may be protected or in the process of being written
                        Log.Trace($"Watcher: unable to delete {targetFile.name} to reduce folder {folderPath} size, either in use or protected.");
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
            Log.Trace("Watcher:: Unexpected exception.");
            Log.Error(exception);
        }
    }

}

internal class FileWatcher : BaseHandler
{
    private TimelineHandler _handler;
    private TimelineEvent _timelineEvent;
    private readonly string _command;
    private readonly string _filePath;
    private static DateTime _lastRead = DateTime.MinValue;

    internal FileWatcher(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    {
        _handler = handler;
        _timelineEvent = timelineEvent;
        _command = command;

        _filePath = timelineEvent.CommandArgs[0].ToString();
        var sleepTime = Convert.ToInt32(timelineEvent.CommandArgs[1]);

        if (timelineEvent.CommandArgs[2] != null)
        {
            var webhookPayload = timelineEvent.CommandArgs[2].ToString();
            this.WebhookCreate(webhookPayload);
        }
            
        if (string.IsNullOrEmpty(_filePath))
        {
            Log.Trace("file path null or empty");
            return;
        }

        var path = string.Empty;
        var file = string.Empty;

        var attr = File.GetAttributes(_filePath);
        if (attr.HasFlag(FileAttributes.Directory))
        {
            path = _filePath;
            Log.Trace($"Directory passed: {path}");
        }
        else
        {
            //MessageBox.Show("Its a file");
            var f = new FileInfo(_filePath);
            path = f.DirectoryName;
            file = f.Name;
            Log.Trace($"File passed - Directory : {path} File: {file}");
        }
            
        var watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };

        if (!string.IsNullOrEmpty(file))
            watcher.Filter = file;

        watcher.EnableRaisingEvents = true;
        watcher.Changed += OnChanged;

        Log.Trace($"Setting up watcher for {_filePath} - watching {path} and filtering on {file}");

        // Begin watching.
        watcher.EnableRaisingEvents = true;

        //keep thread alive forever
        Application.Run();
    }

    // Define the event handlers.
    private void OnChanged(object source, FileSystemEventArgs e)
    {
        // filewatcher throws multiple events, we only need 1
        var lastWriteTime = File.GetLastWriteTime(e.FullPath);
        if (lastWriteTime > _lastRead.AddSeconds(1))
        {
            _lastRead = lastWriteTime;
            Log.Trace("File: " + e.FullPath + " " + e.ChangeType);
                
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
                    
                Report(new ReportItem { Handler = _handler.HandlerType.ToString(), Command = _command, Arg = _filePath, Trackable = _timelineEvent.TrackableId, Result = fileContents});

                if (Program.IsDebug)
                    Console.WriteLine($"File: {e.FullPath} : {e.ChangeType} : {fileContents}");
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }
    }
}