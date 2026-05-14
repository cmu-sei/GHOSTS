// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Handlers;

/// <summary>
/// Remote Desktop Protocol Handler.
/// Opens an RDP session to a randomly chosen target, maintains the connection for a configured
/// execution time, then closes the session and repeats for each timeline event.
/// On Windows uses mstsc, on Linux uses xfreerdp or rdesktop.
/// </summary>
public class Rdp(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    private Credentials _currentCreds;
    private int _jitterFactor;
    private int _executionTime = 20000;
    private int _mouseSleepTime = 10000;
    private int _executionProbability = 100;

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

                if (Handler.HandlerArgs.TryGetValue("execution-time", out var execTimeArg))
                {
                    if (int.TryParse(execTimeArg.ToString(), out var et))
                    {
                        _executionTime = et < 0 ? 20000 : et;
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("mouse-sleep-time", out var mouseSleepArg))
                {
                    if (int.TryParse(mouseSleepArg.ToString(), out var ms))
                    {
                        _mouseSleepTime = ms < 0 ? 10000 : ms;
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("execution-probability", out var execProbArg))
                {
                    if (int.TryParse(execProbArg.ToString(), out var ep))
                    {
                        _executionProbability = (ep < 0 || ep > 100) ? 100 : ep;
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var jitterArg))
                {
                    _jitterFactor = Jitter.JitterFactorParse(jitterArg.ToString());
                }
            }

            if (_currentCreds == null)
            {
                _log.Error("RDP:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
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

            if (timelineEvent.DelayBeforeActual > 0)
                Thread.Sleep(timelineEvent.DelayBeforeActual);

            _log.Trace($"RDP Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    if (_executionProbability < _random.Next(0, 100))
                    {
                        _log.Trace("RDP:: Choice skipped due to execution probability");
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                        continue;
                    }

                    var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                    if (!string.IsNullOrEmpty(cmd.ToString()))
                    {
                        ExecuteRdp(timelineEvent, cmd.ToString());
                    }
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0)
                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
        }
    }

    private void ExecuteRdp(TimelineEvent timelineEvent, string command)
    {
        var charSeparators = new char[] { '|' };
        var cmdArgs = command.Split(charSeparators, 2, StringSplitOptions.None);
        var target = cmdArgs[0];
        var credKey = cmdArgs[1];
        var domain = _currentCreds.GetDomain(credKey);
        var username = _currentCreds.GetUsername(credKey);
        var password = _currentCreds.GetPassword(credKey);

        _log.Trace($"RDP:: Spawning RDP connection for target {target}");

        if (username == null || password == null)
        {
            _log.Error($"RDP:: Missing username or password for credential key '{credKey}', skipping.");
            return;
        }

        Process process = null;
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process = StartWindowsRdp(target);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process = StartLinuxRdp(target, username, password, domain);
            }
            else
            {
                _log.Trace("RDP:: Unsupported platform, skipping.");
                return;
            }

            if (process == null)
            {
                _log.Error($"RDP:: Failed to start RDP process for target {target}");
                return;
            }

            _log.Trace($"RDP:: Connected to {target}, maintaining session for {_executionTime}ms");

            // Maintain the session for the configured execution time, sleeping in intervals
            var totalTime = 0;
            while (totalTime < _executionTime)
            {
                Token.ThrowIfCancellationRequested();
                var sleepTime = Jitter.JitterFactorDelay(_mouseSleepTime, _jitterFactor);
                if (sleepTime <= 0) sleepTime = 1000;
                Thread.Sleep(sleepTime);
                totalTime += sleepTime;
            }

            Report(new ReportItem { Handler = Handler.HandlerType.ToString(), Command = target, Arg = credKey, Trackable = timelineEvent.TrackableId });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _log.Error(e);
        }
        finally
        {
            if (process != null && !process.HasExited)
            {
                _log.Trace($"RDP:: Closing RDP process for target {target}");
                try
                {
                    process.Kill();
                }
                catch (Exception e)
                {
                    _log.Trace($"RDP:: Error killing process: {e.Message}");
                }
            }
            process?.Dispose();
        }
    }

    private static Process StartWindowsRdp(string target)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "mstsc",
            Arguments = $"/v:{target}",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        return Process.Start(startInfo);
    }

    private static Process StartLinuxRdp(string target, string username, string password, string domain)
    {
        // Try xfreerdp first, fall back to rdesktop
        var userArg = domain != null ? $"{domain}\\{username}" : username;

        var xfreerdpPath = FindExecutable("xfreerdp");
        if (xfreerdpPath != null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = xfreerdpPath,
                Arguments = $"/v:{target} /u:{userArg} /p:{password} /cert:ignore",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            return Process.Start(startInfo);
        }

        var rdesktopPath = FindExecutable("rdesktop");
        if (rdesktopPath != null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = rdesktopPath,
                Arguments = $"-u {userArg} -p {password} {target}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            return Process.Start(startInfo);
        }

        _log.Error("RDP:: Neither xfreerdp nor rdesktop found on this system.");
        return null;
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
