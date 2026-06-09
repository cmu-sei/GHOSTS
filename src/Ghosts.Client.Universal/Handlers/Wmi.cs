// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Infrastructure;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Handlers;

public class Wmi(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    private Credentials _currentCreds;
    private int _jitterFactor;
    private int _timeBetweenCommandsMax;
    private int _timeBetweenCommandsMin;

    protected override Task RunOnce()
    {
        try
        {
            if (Handler.HandlerArgs != null)
            {
                if (Handler.HandlerArgs.TryGetValue("CredentialsFile", out var credFileArg))
                {
                    try
                    {
                        _currentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(credFileArg.ToString()));
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("Credentials", out var credentialsArg))
                {
                    try
                    {
                        _currentCreds = JsonConvert.DeserializeObject<Credentials>(credentialsArg.ToString());
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMax", out var maxArg))
                {
                    try
                    {
                        _timeBetweenCommandsMax = int.Parse(maxArg.ToString());
                        if (_timeBetweenCommandsMax < 0) _timeBetweenCommandsMax = 0;
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMin", out var minArg))
                {
                    try
                    {
                        _timeBetweenCommandsMin = int.Parse(minArg.ToString());
                        if (_timeBetweenCommandsMin < 0) _timeBetweenCommandsMin = 0;
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var jitterArg))
                {
                    _jitterFactor = Jitter.JitterFactorParse(jitterArg.ToString());
                }
            }

            if (_currentCreds == null)
            {
                _log.Error("WMI:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
                return Task.CompletedTask;
            }

            Ex();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }

        return Task.CompletedTask;
    }

    private void Ex()
    {
        foreach (var timelineEvent in Handler.TimeLineEvents)
        {
            Token.ThrowIfCancellationRequested();
            WorkingHours.Is(Handler);

            if (timelineEvent.DelayBeforeActual > 0){
                if (Token.WaitHandle.WaitOne(timelineEvent.DelayBeforeActual)) Token.ThrowIfCancellationRequested();

            }

            _log.Trace($"WMI Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                    if (!string.IsNullOrEmpty(cmd.ToString()))
                    {
                        ExecuteWmi(timelineEvent, cmd.ToString());
                    }
                    if (timelineEvent.DelayAfterActual > 0) {
                        if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                    }
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0) {
                if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
            }
        }
    }

    private void ExecuteWmi(TimelineEvent timelineEvent, string command)
    {
        var charSeparators = new char[] { '|' };
        var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
        var hostIp = cmdArgs[0];
        var credKey = cmdArgs[1];
        var wmiCmds = cmdArgs[2].Split(';');
        var domain = _currentCreds.GetDomain(credKey);
        var username = _currentCreds.GetUsername(credKey);
        var password = _currentCreds.GetPassword(credKey);

        _log.Trace($"WMI:: Beginning WMI to host: {hostIp} with command: {command}");

        if (domain == null)
        {
            domain = hostIp;
        }

        if (username == null || password == null)
        {
            _log.Error($"WMI:: Missing username or password for credential key '{credKey}', skipping.");
            return;
        }

        foreach (var wmiCmd in wmiCmds)
        {
            Token.ThrowIfCancellationRequested();
            try
            {
                if (OperatingSystem.IsWindows()) {
                    RunWmicCommand(hostIp, domain, username, password, wmiCmd.Trim());
                } else
                {
                    RunWmiQueryCommand(hostIp, domain, username, password, wmiCmd.Trim());
                }

                if (_timeBetweenCommandsMin > 0 && _timeBetweenCommandsMax > 0 &&
                    _timeBetweenCommandsMin < _timeBetweenCommandsMax)
                {
                    Thread.Sleep(_random.Next(_timeBetweenCommandsMin, _timeBetweenCommandsMax));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                _log.Error(e);
            }
        }

        Report(new ReportItem { Handler = Handler.HandlerType.ToString(), Command = hostIp, Arg = cmdArgs[2], Trackable = timelineEvent.TrackableId });
    }

    private void RunWmicCommand(string host, string domain, string username, string password, string wmiQuery)
    {

        var userArg = $"{domain}\\{username}";
        var arguments = $"/node:{host} /user:{userArg} /password:{password} {wmiQuery}";

        _log.Trace($"WMI:: Executing: wmic /node:{host} /user:{userArg} /password:*** {wmiQuery}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "wmic",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _log.Error("WMI:: Failed to start wmic process.");
            return;
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrWhiteSpace(output))
        {
            _log.Trace($"WMI:: Output: {output.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            _log.Trace($"WMI:: Error output: {error.Trim()}");
        }
    }

    // This uses Python impacket/wmiquery.py , checks that wmiquery.py is on the path before executing
    // The wmiQuery string can be one of the predefined strings used in Ghosts.Client.Windows wmiquery,
    // or it can be a raw query string that is just passed directly to wmiquery.py

    private void RunWmiQueryCommand(string host, string domain, string username, string password, string wmiQuery)
    {

        var exeName = "wmiquery.py";
        var wmiqueryPath = FindExecutable($"{exeName}");
        if (wmiqueryPath == null)
        {
            _log.Info("Python impacket/wmiquery.py must be installed on the path for Linux WMI queries; exiting.");
            return;
        }
        
        var wmiQueryNew = TranslateWmiQuery(wmiQuery);
        var queryFileName = Path.GetTempFileName();
        using (StreamWriter sw = new StreamWriter(queryFileName, false))
        {
            sw.WriteLine(wmiQueryNew);
            sw.WriteLine("exit");
        }
        
        var arguments = $"{domain}/{username}:{password}@{host} -f {queryFileName}";
        

        _log.Trace($"WMI:: Executing: {exeName} {domain}/{username}:***@{host} {wmiQuery}");

        var startInfo = new ProcessStartInfo
        {
            FileName = $"{exeName}",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            _log.Error($"WMI:: Failed to start {exeName} process.");
            return;
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (File.Exists(queryFileName))
        {
            File.Delete(queryFileName);
        }

        if (!string.IsNullOrWhiteSpace(output))
        {
            _log.Trace($"WMI:: Output: {output.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            _log.Trace($"WMI:: Error output: {error.Trim()}");
        }
    }

    private string TranslateWmiQuery(string wmiQuery)
    {
        var newQuery = wmiQuery;

        switch(wmiQuery.ToLower())
        {
            case "getoperatingsystem":
                newQuery = "SELECT * FROM Win32_OperatingSystem";
                break;
            case "getbios":
                newQuery = "SELECT * FROM Win32_BIOS";
                break;
            case "getprocessor":
                newQuery = "SELECT * FROM Win32_Processor";
                break;
            case "getuserlist":
                newQuery = "SELECT * FROM Win32_LoggedOnUser";
                break;
            case "getnetworkinfo":
                newQuery = "SELECT * FROM Win32_NetworkAdapter";
                break;
            case "getprocesslist":
                newQuery = "SELECT * FROM Win32_Process";
                break;
            case "getfileslist":
                newQuery = "SELECT * FROM WIN32_Directory WHERE Name = 'C:\\\\Windows'";
                break;
        }
        return newQuery;
        
    }

    private static string FindExecutable(string name)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = name,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(startInfo);
            if (proc == null) return null;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();
            return proc.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
