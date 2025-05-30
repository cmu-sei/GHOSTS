// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Management.Automation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Ghosts.Client.Comms;
using Ghosts.Domain.Code;
using Ghosts.Domain.Code.Helpers;
using Newtonsoft.Json;
using NLog;

namespace Ghosts.Client.Survey;

public class PowerShellManager
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public List<object> GetObjects(string powershellCommand)
    {
        var list = new List<object>();
        try
        {
            using (var ps1 = PowerShell.Create())
            {
                ps1.AddScript(powershellCommand);
                _log.Trace(powershellCommand);

                var outputCollection = new PSDataCollection<PSObject>();

                outputCollection.DataAdded += outputCollection_DataAdded;
                ps1.Streams.Error.DataAdded += Error_DataAdded;

                var result = ps1.BeginInvoke<PSObject, PSObject>(null, outputCollection);

                // do something else until execution has completed - could be other work
                while (result.IsCompleted == false)
                {
                    Thread.Sleep(1000);
                    // might want to place a timeout here...
                    _log.Trace("Waiting for cmd to complete");
                }

                _log.Trace("Execution has stopped. The pipeline state: " + ps1.InvocationStateInfo.State);

                list.AddRange(outputCollection);
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return list;
    }

    /// <summary>
    /// Event handler for when data is added to the output stream.
    /// </summary>
    /// <param name="sender">Contains the complete PSDataCollection of all output items.</param>
    /// <param name="e">Contains the index ID of the added collection item and the ID of the PowerShell instance this event belongs to.</param>
    private void outputCollection_DataAdded(object sender, DataAddedEventArgs e)
    {
        // do something when an object is written to the output stream
        _log.Trace(sender);
    }

    /// <summary>
    /// Event handler for when Data is added to the Error stream.
    /// </summary>
    /// <param name="sender">Contains the complete PSDataCollection of all error output items.</param>
    /// <param name="e">Contains the index ID of the added collection item and the ID of the PowerShell instance this event belongs to.</param>
    private void Error_DataAdded(object sender, DataAddedEventArgs e)
    {
        // do something when an error is written to the error stream
        _log.Error(sender);
    }
}

public static class SurveyManager
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();

    public static void Run()
    {
        try
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                RunEx();

            }).Start();
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
    }

    private static void RunEx()
    {
        while (true)
        {
            //has file already been generated
            if (Program.Configuration.Survey.Frequency.Equals("once",
                    StringComparison.InvariantCultureIgnoreCase) &&
                File.Exists(ApplicationDetails.InstanceFiles.SurveyResults))
                break;


            _log.Trace("Running new survey...");
            try
            {
                var s = new SurveyResult();
                s.Survey.Created = DateTime.UtcNow;

                if (Guid.TryParse(Program.CheckId.Id, out var g))
                    s.Survey.MachineId = g;

                s.LoadAll();

                var f = new FileInfo(ApplicationDetails.InstanceFiles.SurveyResults).Directory;
                if (f == null)
                {
                    Directory.CreateDirectory(ApplicationDetails.InstanceFiles.SurveyResults);
                }

                try
                {
                    if (File.Exists(ApplicationDetails.InstanceFiles.SurveyResults))
                        File.Delete(ApplicationDetails.InstanceFiles.SurveyResults);
                }
                catch (Exception e)
                {
                    _log.Trace(e);
                }

                var formatting = Formatting.Indented;
                if (Program.Configuration.Survey.OutputFormat.Equals("none",
                        StringComparison.InvariantCultureIgnoreCase))
                    formatting = Formatting.None;

                using (var file = File.CreateText(ApplicationDetails.InstanceFiles.SurveyResults))
                {
                    var serializer = new JsonSerializer
                    {
                        Formatting = formatting,
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    serializer.Serialize(file, s.Survey);
                }
                Updates.PostSurvey();
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }

            Thread.Sleep(Program.Configuration.Survey.CycleSleepMinutes * 60000);
        }
    }
}

public class SurveyResult
{
    private static readonly Logger _log = LogManager.GetCurrentClassLogger();
    private static readonly Random _random = new Random();

    public Domain.Messages.MesssagesForServer.Survey Survey { get; set; }

    private PowerShellManager Ps1;

    public SurveyResult()
    {
        this.Ps1 = new PowerShellManager();
        this.Survey = new Domain.Messages.MesssagesForServer.Survey();
        this.Survey.Uptime = GetUptime();
    }

    public void LoadAll()
    {
        GetNetStatPorts();
        if (!Program.IsDebug)
            Thread.Sleep(_random.Next(500, 900000));

        this.Survey.Interfaces = GetInterfaces();
        if (!Program.IsDebug)
            Thread.Sleep(_random.Next(500, 900000));

        this.Survey.LocalUsers = GetLocalAccounts();
        if (!Program.IsDebug)
            Thread.Sleep(_random.Next(500, 900000));

        this.Survey.Drives = this.GetDriveInfo();
        if (!Program.IsDebug)
            Thread.Sleep(_random.Next(500, 900000));

        this.Survey.Processes = GetProcesses();
        if (!Program.IsDebug)
            Thread.Sleep(_random.Next(500, 900000));

        this.Survey.EventLogs = GetEventLogs();
        if (!Program.IsDebug)
            Thread.Sleep(_random.Next(500, 900000));

        foreach (var item in this.Survey.EventLogs)
        {
            item.Entries = new List<Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry>();
            if (!Program.IsDebug)
                Thread.Sleep(_random.Next(500, 5000));
            foreach (var o in this.GetEventLogEntries(item.Name).ToList())
                item.Entries.Add(o);
        }
    }

    public static TimeSpan GetUptime()
    {
        try
        {
            var mo = new ManagementObject(@"\\.\root\cimv2:Win32_OperatingSystem=@");
            var lastBootUp = ManagementDateTimeConverter.ToDateTime(mo["LastBootUpTime"].ToString());
            return DateTime.Now.ToUniversalTime() - lastBootUp.ToUniversalTime();
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return new TimeSpan();
    }

    public static List<Domain.Messages.MesssagesForServer.Survey.Port> GetNetStatPorts()
    {
        var ports = new List<Domain.Messages.MesssagesForServer.Survey.Port>();
        try
        {
            using (var p = new Process())
            {
                var ps = new ProcessStartInfo();
                ps.Arguments = "-a -n -o";
                ps.FileName = "netstat.exe";
                ps.UseShellExecute = false;
                ps.WindowStyle = ProcessWindowStyle.Hidden;
                ps.RedirectStandardInput = true;
                ps.RedirectStandardOutput = true;
                ps.RedirectStandardError = true;

                p.StartInfo = ps;
                p.Start();

                var stdOutput = p.StandardOutput;
                var stdError = p.StandardError;

                var content = stdOutput.ReadToEnd() + stdError.ReadToEnd();
                var exitStatus = p.ExitCode.ToString();

                if (exitStatus != "0")
                {
                    // Command Errored. Handle Here If Need Be
                }

                //Get The Rows
                var rows = Regex.Split(content, "\r\n");
                foreach (var row in rows)
                {
                    //Split it baby
                    var tokens = Regex.Split(row, "\\s+");
                    if (tokens.Length > 4 && (tokens[1].Equals("UDP") || tokens[1].Equals("TCP")))
                    {
                        var localAddress = Regex.Replace(tokens[2], @"\[(.*?)\]", "1.1.1.1");
                        var foreignAddress = Regex.Replace(tokens[3], @"\[(.*?)\]", "1.1.1.1");
                        ports.Add(new Domain.Messages.MesssagesForServer.Survey.Port
                        {
                            LocalAddress = localAddress.Split(':')[0],
                            LocalPort = localAddress.Split(':')[1],
                            ForeignAddress = foreignAddress.Split(':')[0],
                            ForeignPort = foreignAddress.Split(':')[1],
                            State = tokens[1] == "UDP" ? null : tokens[4],
                            PID = tokens[1] == "UDP" ? Convert.ToInt16(tokens[4]) : Convert.ToInt16(tokens[5]),
                            Protocol = localAddress.Contains("1.1.1.1") ? $"{tokens[1]}v6" : $"{tokens[1]}v4",
                            Process = tokens[1] == "UDP" ? LookupProcess(Convert.ToInt16(tokens[4])) : LookupProcess(Convert.ToInt16(tokens[5]))
                        });
                    }
                }
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return ports;
    }

    public static string LookupProcess(int pid)
    {
        string procName;
        try { procName = Process.GetProcessById(pid).ProcessName; }
        catch (Exception) { procName = "-"; }
        return procName;
    }

    public List<Domain.Messages.MesssagesForServer.Survey.Interface> GetInterfaces()
    {
        var results = new List<Domain.Messages.MesssagesForServer.Survey.Interface>();

        try
        {
            var list = this.Ps1.GetObjects("arp -a");
            var s = new StringBuilder();
            foreach (var o in list)
                s.AppendLine(o.ToString().RemoveDuplicateSpaces());

            var interfaces = s.ToString().Split(new[] { "Interface: " }, StringSplitOptions.None);

            foreach (var iface in interfaces)
            {
                if (string.IsNullOrEmpty(iface.Replace(Environment.NewLine, "")))
                    continue;

                var result = new Domain.Messages.MesssagesForServer.Survey.Interface();
                var i = 0;
                foreach (var line in iface.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                {
                    if (i == 0)
                    {
                        var pos = line.IndexOf("---");
                        result.Name = line.Substring(0, pos).Trim();
                    }
                    else
                    {
                        var o = line.Trim();
                        if (o.StartsWith("Internet Address") || o.Length < 1)
                            continue;

                        else
                        {
                            var vs = o.Split(Convert.ToChar(" "));
                            result.Bindings.Add(new Domain.Messages.MesssagesForServer.Survey.Interface.InterfaceBinding
                            {
                                InternetAddress = vs[0],
                                PhysicalAddress = vs[1],
                                Type = vs[2]
                            });
                        }
                    }
                    i++;
                }
                results.Add(result);
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }

        return results;
    }

    public List<Domain.Messages.MesssagesForServer.Survey.LocalUser> GetLocalAccounts()
    {
        var users = new List<Domain.Messages.MesssagesForServer.Survey.LocalUser>();
        try
        {
            var query = new SelectQuery("Win32_UserAccount");
            var searcher = new ManagementObjectSearcher(query);
            foreach (var user in searcher.Get())
            {
                users.Add(new Domain.Messages.MesssagesForServer.Survey.LocalUser { Username = user["Name"].ToString(), Domain = user["Domain"].ToString() });
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return users;
    }

    public List<Domain.Messages.MesssagesForServer.Survey.DriveInfo> GetDriveInfo()
    {
        var results = new List<Domain.Messages.MesssagesForServer.Survey.DriveInfo>();
        try
        {
            var allDrives = System.IO.DriveInfo.GetDrives();
            foreach (var drive in allDrives)
            {
                var result = new Domain.Messages.MesssagesForServer.Survey.DriveInfo();
                result.AvailableFreeSpace = drive.AvailableFreeSpace;
                result.DriveFormat = drive.DriveFormat;
                result.DriveType = drive.DriveType.ToString();
                result.IsReady = drive.IsReady;
                result.Name = drive.Name;
                result.RootDirectory = drive.RootDirectory.ToString();
                result.TotalFreeSpace = drive.TotalFreeSpace;
                result.TotalSize = drive.TotalSize;
                result.VolumeLabel = drive.VolumeLabel;
                results.Add(result);
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return results;
    }

    public static List<Domain.Messages.MesssagesForServer.Survey.LocalProcess> GetProcesses()
    {
        var results = new List<Domain.Messages.MesssagesForServer.Survey.LocalProcess>();
        try
        {
            foreach (var item in Process.GetProcesses())
            {
                var result = new Domain.Messages.MesssagesForServer.Survey.LocalProcess();
                result.Id = item.Id;
                result.PrivateMemorySize64 = item.PrivateMemorySize64;
                if (!string.IsNullOrEmpty(item.MainWindowTitle))
                    result.MainWindowTitle = item.MainWindowTitle;
                if (!string.IsNullOrEmpty(item.ProcessName))
                    result.ProcessName = item.ProcessName;
                try { result.StartTime = item.StartTime; } catch { }
                if (!string.IsNullOrEmpty(item.StartInfo.FileName))
                    result.FileName = item.StartInfo.FileName;
                if (!string.IsNullOrEmpty(item.StartInfo.UserName))
                    result.Owner = item.StartInfo.UserName;
                if (string.IsNullOrEmpty(result.Owner))
                {
                    var sq = new ObjectQuery("Select * from Win32_Process Where ProcessID = '" + item.Id + "'");
                    var searcher = new ManagementObjectSearcher(sq);
                    if (searcher.Get().Count > 0)
                    {

                        foreach (var managementBaseObject in searcher.Get())
                        {
                            var oReturn = (ManagementObject) managementBaseObject;
                            string[] o = new string[2];
                            //Invoke the method and populate the o var with the user name and domain
                            oReturn.InvokeMethod("GetOwner", o);
                            result.Owner = o[0];
                            if (!string.IsNullOrEmpty(o[1]))
                                result.OwnerDomain = o[1];

                            var sid = new string[1];
                            oReturn.InvokeMethod("GetOwnerSid", sid);
                            result.OwnerSid = sid[0];
                        }
                    }
                }
                if (!results.Exists(x => x.ProcessName == item.ProcessName))
                    results.Add(result);
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return results;
    }

    public List<Domain.Messages.MesssagesForServer.Survey.EventLog> GetEventLogs()
    {
        var results = new List<Domain.Messages.MesssagesForServer.Survey.EventLog>();
        try
        {
            var list = this.Ps1.GetObjects("Get-EventLog -List");
            var olist = list.Select(o => ((EventLog)((PSObject)o).BaseObject).Log);

            foreach (var item in olist)
                results.Add(new Domain.Messages.MesssagesForServer.Survey.EventLog { Name = item });

        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return results;
    }

    public List<Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry> GetEventLogEntries(string logName)
    {
        var results = new List<Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry>();
        try
        {
            var list = this.Ps1.GetObjects($"Get-EventLog -logname {logName} -newest 50");
            foreach (var item in list)
            {
                var o = ((EventLogEntry)((PSObject)item).BaseObject);
                var log = new Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry();
                log.Created = o.TimeGenerated;
                log.EntryType = o.EntryType.ToString();
                log.Message = o.Message;
                log.Source = o.Source;
                results.Add(log);
            }
        }
        catch (Exception e)
        {
            _log.Trace(e);
        }
        return results;
    }
}