// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using Ghosts.Domain;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Ghosts.Client.Handlers
{
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
                Infrastructure.WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                {
                    Thread.Sleep(timelineEvent.DelayBefore);
                }

                Log.Trace($"Watcher: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    //watch a folder, each handler watches one folder
                    case "folder":
                        var fw = new FolderWatcher(handler, timelineEvent, timelineEvent.Command);
                        break;
                    //File
                    default:
                        var w = new FileWatcher(handler, timelineEvent, timelineEvent.Command);
                        break;
                }

                if (timelineEvent.DelayAfter > 0)
                {
                    Thread.Sleep(timelineEvent.DelayAfter);
                }
            }
        }
    }

    internal class Afile 
    {
        public string name { get; set; }
        public long size { get; set; }
        public DateTime ctime { get; set; }

        //default sort method uses only the size
        public static int CompareBySize(Afile x, Afile y)
        {
            // A null value means that this object is greater.
            if (y == null || x.size > y.size)
                return 1;
            else if (x.size == y.size) return 0;
            else return -1;
        }

        public static int CompareByDate(Afile x, Afile y)
        {
            // A null value means that this object is greater.
            if (y == null || x.ctime > y.ctime)
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

        static void GetAllFiles(string folder, List<Afile> allfiles )
        {
            // Get array of all file names.
            string[] filelist = Directory.GetFiles(folder, "*");
            foreach (string fname in filelist)
            {
                FileInfo info = new FileInfo(fname);
                allfiles.Add(new Afile() { name = info.FullName, size = info.Length, ctime = info.LastWriteTime });
            }
            //get subdirectories
            string[] dirlist = Directory.GetDirectories(folder, "*");
            foreach (string dname in dirlist)
            {
                GetAllFiles(dname,allfiles);
            }
        }

        //returns total directory size including sub directories
        static long GetDirectorySize(string folder)
        {
            // Get array of all file names.
            string[] filelist = Directory.GetFiles(folder, "*");

            // Sum file sizes
            long size = 0;
            foreach (string fname in filelist)
            {
                FileInfo info = new FileInfo(fname);
                size += info.Length;
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
                        if (words[0] == "path") folderPath = words[1];
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

        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // ignore if size is shrinking
            var currentSize = GetDirectorySize(folderPath);
            if (currentSize > lastFolderSize && currentSize > folderMaxSize)
            {
                //size has grown, exceeds max, need to delete
                //get a list of all files with their sizes
                List<Afile> allfiles = new List<Afile>();
                GetAllFiles(folderPath, allfiles);

                while (true) {
                    Afile targetFile = null;
                    
                    if (deletionApproach == "oldest")
                    {
                        allfiles.Sort(Afile.CompareByDate);
                        targetFile = allfiles[0];
                        allfiles.RemoveAt(0);
                    }
                    else if (deletionApproach == "largest")
                    {
                        allfiles.Sort(Afile.CompareBySize);
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
                        if (currentSize < folderMaxSize) break;  //break from loop, we have hit the target size
                    }
                    catch (Exception deletionException)
                    {
                        //ignore the exception, file may be protected
                        Log.Trace($"Watcher: unable to delete {targetFile.name} to reduce folder {folderPath} size, exception {deletionException}");
                    }
                    if (allfiles.Count == 0)
                    {
                        //no more files to try to delete and loop is still running. 
                        //disable any future execution
                        watcher.EnableRaisingEvents = false;
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
                    
                    this.Report(_handler.HandlerType.ToString(), _command, _filePath, _timelineEvent.TrackableId, fileContents);

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
}