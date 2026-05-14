// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using Renci.SshNet;

/*
 * Used Package Renci.sshNet
 * Installed via packag manager
 * Install-Package SSH.NET
 */

namespace Ghosts.Client.Universal.Handlers;

    /// <summary>
    /// This handler connects to a remote host and executes SSH commands.
    /// This uses the Credentials class that keeps a simple dictionary of credentials
    /// For SSH, is  uses the Renci.sshNet package, install via PM with Install-Package SSH.NET
    /// The SSH connection uses a ShellStream for executing commands once a connection is established.
    /// Each SSH shell command can have reserved words in it, such as '[remotedirectory]' which
    /// is replaced by a random remote directory on the server.  So 'cd [remotedirectory]' will change
    /// to a random remote directory.
    /// See the Sample Timelines/Ssh.json for a sample timeline using this handler.
    /// </summary>
    public class Ssh(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        private Credentials _currentCreds;
        private SshSupport _currentSshSupport;
        private int _jitterFactor;

        protected override Task RunOnce()
        {
            try
            {
                _currentSshSupport = new SshSupport();
                if (Handler.HandlerArgs != null)
                {
                    if (Handler.HandlerArgs.TryGetValue("CredentialsFile", out var v1))
                    {
                        try
                        {
                            _currentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(v1.ToString()));
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

                    if (Handler.HandlerArgs.TryGetValue("ValidExts", out var v2))
                    {
                        try
                        {
                            _currentSshSupport.ValidExts = v2.ToString().Split(';');
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }

                    if (Handler.HandlerArgs.TryGetValue("CommandTimeout", out var v3))
                    {
                        try
                        {
                            _currentSshSupport.CommandTimeout = int.Parse(v3.ToString());
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }

                    if (Handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMax", out var v4))
                    {
                        try
                        {
                            _currentSshSupport.TimeBetweenCommandsMax = int.Parse(v4.ToString());
                            if (_currentSshSupport.TimeBetweenCommandsMax < 0) _currentSshSupport.TimeBetweenCommandsMax = 0;
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }

                    if (Handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMin", out var v5))
                    {
                        try
                        {
                            _currentSshSupport.TimeBetweenCommandsMin = int.Parse(v5.ToString());
                            if (_currentSshSupport.TimeBetweenCommandsMin < 0) _currentSshSupport.TimeBetweenCommandsMin = 0;
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }

                    if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var v6))
                    {
                        _jitterFactor = Jitter.JitterFactorParse(v6.ToString());
                    }
                }

                if (_currentCreds == null)
                {
                    _log.Error("SSH:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
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

                _log.Trace($"SSH Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            ExecuteCommand(timelineEvent, cmd.ToString());
                        }
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                        break;
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
            }
        }

        private void ExecuteCommand(TimelineEvent timelineEvent, string command)
        {
            var charSeparators = new char[] { '|' };
            var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
            var hostIp = cmdArgs[0];
            var credKey = cmdArgs[1];
            var sshCmds = cmdArgs[2].Split(';');
            var username = _currentCreds.GetUsername(credKey);
            var password = _currentCreds.GetPassword(credKey);
            _log.Trace("Beginning SSH to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {
                using (var client = new SshClient(hostIp, username, password))
                {
                    try
                    {
                        client.Connect();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        return;
                    }

                    _currentSshSupport.HostIp = hostIp;
                    var shellStreamSsh = client.CreateShellStream("vt220", 80, 60, 800, 600, 65536);
                    // flush the input of welcome login text
                    _currentSshSupport.GetSshCommandOutput(shellStreamSsh, true);

                    foreach (var sshCmd in sshCmds)
                    {
                        Token.ThrowIfCancellationRequested();
                        try
                        {
                            _currentSshSupport.RunSshCommand(shellStreamSsh, sshCmd.Trim());
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

                    client.Disconnect();
                    Report(new ReportItem { Handler = Handler.HandlerType.ToString(), Command = hostIp, Arg = cmdArgs[2], Trackable = timelineEvent.TrackableId });
                }
            }
        }
    }
