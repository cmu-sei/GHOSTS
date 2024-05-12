using System;
using System.IO;
using System.Threading;
using Renci.SshNet;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Newtonsoft.Json;
using Ghosts.Domain.Code;

namespace Ghosts.Client.Handlers
{
    public class Sftp : BaseHandler
    {

        private Credentials CurrentCreds = null;
        private SftpSupport CurrentSftpSupport = null;   //current SftpSupport for this object
        public int jitterfactor = 0;


        public Sftp(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                this.CurrentSftpSupport = new SftpSupport();
                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.ContainsKey("CredentialsFile"))
                    {
                        try
                        {
                            this.CurrentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(handler.HandlerArgs["CredentialsFile"].ToString()));
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }

                    if (handler.HandlerArgs.ContainsKey("TimeBetweenCommandsMax"))
                    {
                        try
                        {
                            this.CurrentSftpSupport.TimeBetweenCommandsMax = Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMax"].ToString());
                            if (this.CurrentSftpSupport.TimeBetweenCommandsMax < 0) this.CurrentSftpSupport.TimeBetweenCommandsMax = 0;
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("TimeBetweenCommandsMin"))
                    {
                        try
                        {
                            this.CurrentSftpSupport.TimeBetweenCommandsMin = Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMin"].ToString());
                            if (this.CurrentSftpSupport.TimeBetweenCommandsMin < 0) this.CurrentSftpSupport.TimeBetweenCommandsMin = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("UploadDirectory"))
                    {
                        string targetDir = handler.HandlerArgs["UploadDirectory"].ToString();
                        targetDir = Environment.ExpandEnvironmentVariables(targetDir);
                        if (!Directory.Exists(targetDir))
                        {
                            Log.Trace($"Sftp:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        }
                        else
                        {
                            this.CurrentSftpSupport.uploadDirectory = targetDir;
                        }
                    }

                    if (this.CurrentSftpSupport.uploadDirectory == null)
                    {
                        this.CurrentSftpSupport.uploadDirectory = KnownFolders.GetDownloadFolderPath();
                    }

                    this.CurrentSftpSupport.downloadDirectory = KnownFolders.GetDownloadFolderPath();

                    if (handler.HandlerArgs.ContainsKey("delay-jitter"))
                    {
                        jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
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
                Log.Trace("Sftp closing...");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }

        }
        public void Ex(TimelineHandler handler)
        {
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                Log.Trace($"Sftp Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            this.Command(handler, timelineEvent, cmd.ToString());
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

            char[] charSeparators = new char[] { '|' };
            var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
            var hostIp = cmdArgs[0];
            this.CurrentSftpSupport.HostIp = hostIp; //for trace output
            var credKey = cmdArgs[1];
            var sftpCmds = cmdArgs[2].Split(';');
            var username = this.CurrentCreds.GetUsername(credKey);
            var password = this.CurrentCreds.GetPassword(credKey);
            Log.Trace("Beginning Sftp to host:  " + hostIp + " with command: " + command);

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
                        Log.Error(e);
                        return;  //unable to connect
                    }
                    //we are connected, execute the commands


                    foreach (var sftpCmd in sftpCmds)
                    {
                        try
                        {
                            this.CurrentSftpSupport.RunSftpCommand(client, sftpCmd.Trim());
                            if (this.CurrentSftpSupport.TimeBetweenCommandsMin != 0 && this.CurrentSftpSupport.TimeBetweenCommandsMax != 0 && this.CurrentSftpSupport.TimeBetweenCommandsMin < this.CurrentSftpSupport.TimeBetweenCommandsMax)
                            {
                                Thread.Sleep(_random.Next(this.CurrentSftpSupport.TimeBetweenCommandsMin, this.CurrentSftpSupport.TimeBetweenCommandsMax));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e); //some error occurred during this command, try the next one
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
