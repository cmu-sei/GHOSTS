using System;
using System.IO;
using System.Threading;
using ghosts.client.linux.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using Renci.SshNet;

namespace ghosts.client.linux.handlers
{
    public class Sftp : BaseHandler
    {

        private readonly Credentials CurrentCreds = null;
        private readonly SftpSupport CurrentSftpSupport = null;   //current SftpSupport for this object
        public int jitterfactor = 0;


        public Sftp(TimelineHandler handler)
        {
            try
            {
                Init(handler);
                CurrentSftpSupport = new SftpSupport();
                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.TryGetValue("CredentialsFile", out var v1))
                    {
                        try
                        {
                            CurrentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(v1.ToString()));
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }

                    if (handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMax", out var v2))
                    {
                        try
                        {
                            CurrentSftpSupport.TimeBetweenCommandsMax = int.Parse(v2.ToString());
                            if (CurrentSftpSupport.TimeBetweenCommandsMax < 0) CurrentSftpSupport.TimeBetweenCommandsMax = 0;
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMin", out var v3))
                    {
                        try
                        {
                            CurrentSftpSupport.TimeBetweenCommandsMin = int.Parse(v3.ToString());
                            if (CurrentSftpSupport.TimeBetweenCommandsMin < 0) CurrentSftpSupport.TimeBetweenCommandsMin = 0;
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("UploadDirectory", out var v4))
                    {
                        var targetDir = v4.ToString();
                        targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                        if (!Directory.Exists(targetDir))
                        {
                            _log.Trace($"Sftp:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        }
                        else
                        {
                            CurrentSftpSupport.uploadDirectory = targetDir;
                        }
                    }

                    CurrentSftpSupport.uploadDirectory ??= KnownFolders.GetDownloadFolderPath();

                    CurrentSftpSupport.downloadDirectory = KnownFolders.GetDownloadFolderPath();

                    if (handler.HandlerArgs.TryGetValue("delay-jitter", out var value))
                    {
                        jitterfactor = Jitter.JitterFactorParse(value.ToString());
                    }
                }



                if (handler.Loop)
                {
                    while (true)
                    {
                        Ex(handler);
                    }
                }
                else
                {
                    Ex(handler);
                }
            }
            catch (ThreadAbortException)
            {
                _log.Trace("Sftp closing...");
            }
            catch (Exception e)
            {
                _log.Error(e);
            }

        }
        public void Ex(TimelineHandler handler)
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                _log.Trace($"Sftp Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            Command(handler, timelineEvent, cmd.ToString());
                        }
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                        break;
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor)); ;
            }
        }


        public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
        {

            var charSeparators = new char[] { '|' };
            var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
            var hostIp = cmdArgs[0];
            CurrentSftpSupport.HostIp = hostIp; //for trace output
            var credKey = cmdArgs[1];
            var sftpCmds = cmdArgs[2].Split(';');
            var username = CurrentCreds.GetUsername(credKey);
            var password = CurrentCreds.GetPassword(credKey);
            _log.Trace("Beginning Sftp to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {

                //have IP, user/pass, try connecting
                using (var client = new SftpClient(hostIp, username, password))
                {
                    try
                    {
                        client.Connect();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        return;  //unable to connect
                    }
                    //we are connected, execute the commands


                    foreach (var sftpCmd in sftpCmds)
                    {
                        try
                        {
                            CurrentSftpSupport.RunSftpCommand(client, sftpCmd.Trim());
                            if (CurrentSftpSupport.TimeBetweenCommandsMin != 0 && CurrentSftpSupport.TimeBetweenCommandsMax != 0 && CurrentSftpSupport.TimeBetweenCommandsMin < CurrentSftpSupport.TimeBetweenCommandsMax)
                            {
                                Thread.Sleep(_random.Next(CurrentSftpSupport.TimeBetweenCommandsMin, CurrentSftpSupport.TimeBetweenCommandsMax));
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Error(e); //some error occurred during this command, try the next one
                        }
                    }
                    client.Disconnect();
                    client.Dispose();
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = hostIp, Arg = cmdArgs[2], Trackable = timelineEvent.TrackableId });
                }
            }
        }
    }
}
