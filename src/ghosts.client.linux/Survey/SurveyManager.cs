// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using ghosts.client.linux.Comms;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using NLog;

namespace ghosts.client.linux.Survey
{
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
                    var s = new SurveyResult
                    {
                        Survey =
                        {
                            Created = DateTime.UtcNow
                        }
                    };

                    if (Guid.TryParse(Program.CheckId.Id, out var g))
                        s.Survey.MachineId = g;

                    s.LoadAll();

                    var f = new FileInfo(ApplicationDetails.InstanceFiles.SurveyResults);
                    if (f.Directory is { Exists: false })
                    {
                        Directory.CreateDirectory(f.DirectoryName);
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

    public partial class SurveyResult
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        public Ghosts.Domain.Messages.MesssagesForServer.Survey Survey { get; set; }

        public SurveyResult()
        {
            Survey = new Ghosts.Domain.Messages.MesssagesForServer.Survey
            {
                Uptime = GetUptime()
            };
        }

        public void LoadAll()
        {
            var random = new Random();

            Survey.Ports = GetNetStatPorts();
            if (!Program.IsDebug)
                Thread.Sleep(random.Next(500, 900000));

            Survey.Interfaces = GetInterfaces();
            if (!Program.IsDebug)
                Thread.Sleep(random.Next(500, 900000));

            Survey.LocalUsers = GetLocalAccounts();
            if (!Program.IsDebug)
                Thread.Sleep(random.Next(500, 900000));

            Survey.Drives = GetDriveInfo();
            if (!Program.IsDebug)
                Thread.Sleep(random.Next(500, 900000));

            Survey.Processes = GetProcesses();
            if (!Program.IsDebug)
                Thread.Sleep(random.Next(500, 900000));

            Survey.EventLogs = GetEventLogs();
            if (!Program.IsDebug)
                Thread.Sleep(random.Next(500, 900000));

            foreach (var item in Survey.EventLogs)
            {
                item.Entries = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry>();
                if (!Program.IsDebug)
                    Thread.Sleep(random.Next(500, 5000));
                foreach (var o in GetEventLogEntries(item.Name))
                    item.Entries.Add(o);
            }
        }

        public static TimeSpan GetUptime()
        {
            try
            {
                return TimeSpan.FromMilliseconds(Stopwatch.GetTimestamp());
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
            return new TimeSpan();
        }

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.Port> GetNetStatPorts()
        {
            var ports = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.Port>();

            try
            {
                using var p = new Process();
                var ps = new ProcessStartInfo
                {
                    Arguments = "-a -n -o",
                    FileName = "netstat.exe",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

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
                var rows = MyRegex().Split(content);
                foreach (var row in rows)
                {
                    //Split it
                    var tokens = Regex.Split(row, "\\s+");
                    if (tokens.Length > 4 && (tokens[1].Equals("UDP") || tokens[1].Equals("TCP")))
                    {
                        var localAddress = Regex.Replace(tokens[2], @"\[(.*?)\]", "1.1.1.1");
                        var foreignAddress = Regex.Replace(tokens[3], @"\[(.*?)\]", "1.1.1.1");
                        ports.Add(new Ghosts.Domain.Messages.MesssagesForServer.Survey.Port
                        {
                            LocalAddress = localAddress.Split(':')[0],
                            LocalPort = localAddress.Split(':')[1],
                            ForeignAddress = foreignAddress.Split(':')[0],
                            ForeignPort = foreignAddress.Split(':')[1],
                            State = tokens[1] == "UDP" ? null : tokens[4],
                            PID = tokens[1] == "UDP" ? Convert.ToInt16(tokens[4]) : Convert.ToInt16(tokens[5]),
                            Protocol = localAddress.Contains("1.1.1.1") ? string.Format("{0}v6", tokens[1]) : string.Format("{0}v4", tokens[1]),
                            Process = tokens[1] == "UDP" ? LookupProcess(Convert.ToInt16(tokens[4])) : LookupProcess(Convert.ToInt16(tokens[5]))
                        });
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

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.Interface> GetInterfaces()
        {
            var results = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.Interface>();

            try
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces();
                var adapterCount = 0;
                foreach (var adapter in adapters)
                {
                    adapterCount += 1;
                    var iFace = new Ghosts.Domain.Messages.MesssagesForServer.Survey.Interface
                    {
                        Name = adapter.Name,
                        Id = adapterCount
                    };

                    var physicalAddress = adapter.GetPhysicalAddress();
                    var ipProperties = adapter.GetIPProperties();
                    if (ipProperties.UnicastAddresses != null)
                    {
                        foreach (var address in ipProperties.UnicastAddresses)
                        {
                            var bind = new Ghosts.Domain.Messages.MesssagesForServer.Survey.Interface.InterfaceBinding
                            {
                                Type = adapter.NetworkInterfaceType.ToString(),
                                InternetAddress = address.Address.ToString(),
                                PhysicalAddress = physicalAddress.ToString()
                            };
                            iFace.Bindings.Add(bind);
                        }
                    }

                    results.Add(iFace);
                }
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }

            return results;
        }

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.LocalUser> GetLocalAccounts()
        {
            var users = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.LocalUser>();
            try
            {

            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
            return users;
        }

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.DriveInfo> GetDriveInfo()
        {
            var results = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.DriveInfo>();
            try
            {
                var allDrives = System.IO.DriveInfo.GetDrives();
                foreach (var drive in allDrives)
                {
                    var result = new Ghosts.Domain.Messages.MesssagesForServer.Survey.DriveInfo
                    {
                        AvailableFreeSpace = drive.AvailableFreeSpace,
                        DriveFormat = drive.DriveFormat,
                        DriveType = drive.DriveType.ToString(),
                        IsReady = drive.IsReady,
                        Name = drive.Name,
                        RootDirectory = drive.RootDirectory.ToString(),
                        TotalFreeSpace = drive.TotalFreeSpace,
                        TotalSize = drive.TotalSize,
                        VolumeLabel = drive.VolumeLabel
                    };
                    results.Add(result);
                }
            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
            return results;
        }

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.LocalProcess> GetProcesses()
        {
            var results = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.LocalProcess>();
            try
            {
                foreach (var item in Process.GetProcesses())
                {
                    var result = new Ghosts.Domain.Messages.MesssagesForServer.Survey.LocalProcess();

                    if (!string.IsNullOrEmpty(item.MainWindowTitle))
                        result.MainWindowTitle = item.MainWindowTitle;
                    if (!string.IsNullOrEmpty(item.ProcessName))
                        result.ProcessName = item.ProcessName;
                    try { result.StartTime = item.StartTime; }
                    catch
                    {
                        // ignore
                    }
                    if (!string.IsNullOrEmpty(item.StartInfo.FileName))
                        result.FileName = item.StartInfo.FileName;
                    if (!string.IsNullOrEmpty(item.StartInfo.UserName))
                        result.Owner = item.StartInfo.UserName;
                    if (string.IsNullOrEmpty(result.Owner))
                    {
                        // how to get owners on linux?
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

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.EventLog> GetEventLogs()
        {
            var results = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.EventLog>();
            try
            {

            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
            return results;
        }

        public static List<Ghosts.Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry> GetEventLogEntries(string logName)
        {
            var results = new List<Ghosts.Domain.Messages.MesssagesForServer.Survey.EventLog.EventLogEntry>();
            try
            {

            }
            catch (Exception e)
            {
                _log.Trace(e);
            }
            return results;
        }

        [GeneratedRegex("\r\n")]
        private static partial Regex MyRegex();
    }
}
