using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using Renci.SshNet;

namespace Ghosts.Client.Universal.Handlers;

public class Sftp(Timeline timeline, TimelineHandler handler, CancellationToken token)
: BaseHandler(timeline, handler, token)
{
    private Credentials _currentCreds;
    private SftpSupport _currentSftpSupport;
    private int _jitterFactor;

    protected override Task RunOnce()
    {
        try
        {
            _currentSftpSupport = new SftpSupport();
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

                if (Handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMax", out var v2))
                {
                    try
                    {
                        _currentSftpSupport.TimeBetweenCommandsMax = int.Parse(v2.ToString());
                        if (_currentSftpSupport.TimeBetweenCommandsMax < 0)
                            _currentSftpSupport.TimeBetweenCommandsMax = 0;
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMin", out var v3))
                {
                    try
                    {
                        _currentSftpSupport.TimeBetweenCommandsMin = int.Parse(v3.ToString());
                        if (_currentSftpSupport.TimeBetweenCommandsMin < 0)
                            _currentSftpSupport.TimeBetweenCommandsMin = 0;
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }
                }

                if (Handler.HandlerArgs.TryGetValue("UploadDirectory", out var v4))
                {
                    var targetDir = v4.ToString();
                    targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                    if (!Directory.Exists(targetDir))
                    {
                        _log.Trace($"Sftp:: upload directory {targetDir} does not exist, using browser downloads directory.");
                    }
                    else
                    {
                        _currentSftpSupport.uploadDirectory = targetDir;
                    }
                }

                _currentSftpSupport.uploadDirectory ??= KnownFolders.GetDownloadFolderPath();
                _currentSftpSupport.downloadDirectory = KnownFolders.GetDownloadFolderPath();

                if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var value))
                {
                    _jitterFactor = Jitter.JitterFactorParse(value.ToString());
                }
            }

            if (_currentCreds == null)
            {
                _log.Error("Sftp:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
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

            _log.Trace($"Sftp Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

            switch (timelineEvent.Command)
            {
                case "random":
                    var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                    if (!string.IsNullOrEmpty(cmd.ToString()))
                    {
                        ExecuteCommand(timelineEvent, cmd.ToString());
                    }
                    if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
                    break;
            }

            if (timelineEvent.DelayAfterActual > 0) {
                if (Token.WaitHandle.WaitOne(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor))) Token.ThrowIfCancellationRequested();
            }
        }
    }

    private void ExecuteCommand(TimelineEvent timelineEvent, string command)
    {
        var charSeparators = new char[] { '|' };
        var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
        var hostIp = cmdArgs[0];
        _currentSftpSupport.HostIp = hostIp;
        var credKey = cmdArgs[1];
        var sftpCmds = cmdArgs[2].Split(';');
        var username = _currentCreds.GetUsername(credKey);
        var password = _currentCreds.GetPassword(credKey);
        _log.Trace("Beginning Sftp to host:  " + hostIp + " with command: " + command);

        if (username != null && password != null)
        {
            using (var client = new SftpClient(hostIp, username, password))
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

                foreach (var sftpCmd in sftpCmds)
                {
                    Token.ThrowIfCancellationRequested();
                    try
                    {
                        _currentSftpSupport.RunSftpCommand(client, sftpCmd.Trim());
                        if (_currentSftpSupport.TimeBetweenCommandsMin != 0 &&
                            _currentSftpSupport.TimeBetweenCommandsMax != 0 &&
                            _currentSftpSupport.TimeBetweenCommandsMin < _currentSftpSupport.TimeBetweenCommandsMax)
                        {
                            if (Token.WaitHandle.WaitOne(_random.Next(_currentSftpSupport.TimeBetweenCommandsMin, _currentSftpSupport.TimeBetweenCommandsMax))) Token.ThrowIfCancellationRequested();
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

                client.Disconnect();
                Report(new ReportItem
                {
                    Handler = Handler.HandlerType.ToString(),
                    Command = hostIp,
                    Arg = cmdArgs[2],
                    Trackable = timelineEvent.TrackableId
                });
            }
        }
    }
}
