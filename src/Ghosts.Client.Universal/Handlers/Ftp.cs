// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Client.Infrastructure;
using Ghosts.Client.Universal.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;

namespace Ghosts.Client.Universal.Handlers
{
    public class Ftp(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
        : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
    {
        private Credentials _currentCreds;
        private FtpSupport _currentFtpSupport;
        private int _jitterFactor;

        protected override Task RunOnce()
        {
            try
            {
                _currentFtpSupport = new FtpSupport();
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

                    if (Handler.HandlerArgs.TryGetValue("UploadDirectory", out var v2))
                    {
                        var targetDir = v2.ToString();
                        targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                        if (!Directory.Exists(targetDir))
                        {
                            _log.Trace($"Ftp:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        }
                        else
                        {
                            _currentFtpSupport.uploadDirectory = targetDir;
                        }
                    }

                    _currentFtpSupport.uploadDirectory ??= KnownFolders.GetDownloadFolderPath();
                    _currentFtpSupport.downloadDirectory = KnownFolders.GetDownloadFolderPath();

                    if (Handler.HandlerArgs.TryGetValue("delay-jitter", out var v3))
                    {
                        _jitterFactor = Jitter.JitterFactorParse(v3.ToString());
                    }

                    if (Handler.HandlerArgs.TryGetValue("deletion-probability", out var v4))
                    {
                        if (int.TryParse(v4.ToString(), out var delProb))
                        {
                            _currentFtpSupport.deletionProbability = delProb;
                            if (!CheckProbabilityVar("deletion-probability", _currentFtpSupport.deletionProbability))
                            {
                                _currentFtpSupport.deletionProbability = 20;
                            }
                        }
                    }

                    if (Handler.HandlerArgs.TryGetValue("download-probability", out var v5))
                    {
                        if (int.TryParse(v5.ToString(), out var dlProb))
                        {
                            _currentFtpSupport.downloadProbability = dlProb;
                            if (!CheckProbabilityVar("download-probability", _currentFtpSupport.downloadProbability))
                            {
                                _currentFtpSupport.downloadProbability = 40;
                            }
                        }
                    }

                    if (Handler.HandlerArgs.TryGetValue("upload-probability", out var v6))
                    {
                        if (int.TryParse(v6.ToString(), out var ulProb))
                        {
                            _currentFtpSupport.uploadProbability = ulProb;
                            if (!CheckProbabilityVar("upload-probability", _currentFtpSupport.uploadProbability))
                            {
                                _currentFtpSupport.uploadProbability = 40;
                            }
                        }
                    }

                    if ((_currentFtpSupport.deletionProbability + _currentFtpSupport.uploadProbability + _currentFtpSupport.downloadProbability) > 100)
                    {
                        _log.Trace("Ftp:: Sum of deletion/upload/download probabilities > 100, setting to defaults.");
                        _currentFtpSupport.uploadProbability = 40;
                        _currentFtpSupport.downloadProbability = 40;
                        _currentFtpSupport.deletionProbability = 20;
                    }
                }

                if (_currentCreds == null)
                {
                    _log.Error("Ftp:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
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

                _log.Trace($"Ftp Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");
                int[] probabilityList = { _currentFtpSupport.uploadProbability, _currentFtpSupport.downloadProbability, _currentFtpSupport.deletionProbability };
                string[] actionList = { "upload", "download", "delete" };

                switch (timelineEvent.Command)
                {
                    case "random":
                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            var action = SelectActionFromProbabilities(probabilityList, actionList);
                            ExecuteCommand(timelineEvent, cmd.ToString(), action);
                        }
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
                        break;
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, _jitterFactor));
            }
        }

        private void ExecuteCommand(TimelineEvent timelineEvent, string command, string action)
        {
            var charSeparators = new char[] { '|' };
            var cmdArgs = command.Split(charSeparators, 2, StringSplitOptions.None);
            var hostIp = cmdArgs[0];
            _currentFtpSupport.HostIp = hostIp;
            var credKey = cmdArgs[1];
            var username = _currentCreds.GetUsername(credKey);
            var password = _currentCreds.GetPassword(credKey);
            _log.Trace("Beginning Ftp to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {
                try
                {
                    var netcred = new NetworkCredential(username, password);

                    try
                    {
                        _currentFtpSupport.RunFtpCommand(hostIp, netcred, action);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                    }

                    Report(new ReportItem { Handler = Handler.HandlerType.ToString(), Command = hostIp, Arg = action, Trackable = timelineEvent.TrackableId });
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
        }

        private static bool CheckProbabilityVar(string name, int value)
        {
            if (!(value >= 0 && value <= 100))
            {
                _log.Trace($"Variable {name} with value {value} must be an int between 0 and 100, setting to 0");
                return false;
            }
            return true;
        }

        internal static string SelectActionFromProbabilities(int[] probabilityList, string[] actionList)
        {
            int choice = _random.Next(0, 101);
            int endRange;
            int startRange = 0;
            int index = 0;
            foreach (var probability in probabilityList)
            {
                if (probability > 0)
                {
                    endRange = startRange + probability;
                    if (choice >= startRange && choice <= endRange) return actionList[index];
                    else startRange = endRange + 1;
                }
                index++;
            }

            return null;
        }
    }
}
