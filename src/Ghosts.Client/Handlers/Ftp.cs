using System;
using System.IO;
using System.Threading;
using Renci.SshNet;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Newtonsoft.Json;
using Ghosts.Domain.Code;
using WorkingHours = Ghosts.Client.Infrastructure.WorkingHours;
using System.Net;

namespace Ghosts.Client.Handlers
{
    public class Ftp : BaseHandler
    {

        private Credentials CurrentCreds = null;
        private FtpSupport CurrentFtpSupport = null;   //current FtpSupport for this object

        public int jitterfactor = 0;

        public Ftp(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                this.CurrentFtpSupport = new FtpSupport();
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
                            this.CurrentFtpSupport.TimeBetweenCommandsMax = Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMax"].ToString());
                            if (this.CurrentFtpSupport.TimeBetweenCommandsMax < 0) this.CurrentFtpSupport.TimeBetweenCommandsMax = 0;
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
                            this.CurrentFtpSupport.TimeBetweenCommandsMin = Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMin"].ToString());
                            if (this.CurrentFtpSupport.TimeBetweenCommandsMin < 0) this.CurrentFtpSupport.TimeBetweenCommandsMin = 0;
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
                            Log.Trace($"Ftp:: upload directory {targetDir} does not exist, using browser downloads directory.");
                        }
                        else
                        {
                            this.CurrentFtpSupport.uploadDirectory = targetDir;
                        }
                    }

                    if (this.CurrentFtpSupport.uploadDirectory == null)
                    {
                        this.CurrentFtpSupport.uploadDirectory = KnownFolders.GetDownloadFolderPath();
                    }

                    this.CurrentFtpSupport.downloadDirectory = KnownFolders.GetDownloadFolderPath();

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
                Log.Trace("Ftp closing...");
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

                if (timelineEvent.DelayBefore > 0)
                    Thread.Sleep(timelineEvent.DelayBefore);

                Log.Trace($"Ftp Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        while (true)
                        {
                            var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                            {
                                this.Command(handler, timelineEvent, cmd.ToString());
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor)); ;
                        }
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor)); ;
            }
        }


        public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
        {

            char[] charSeparators = new char[] { '|' };
            var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
            var hostIp = cmdArgs[0];
            this.CurrentFtpSupport.HostIp = hostIp; //for trace output
            var credKey = cmdArgs[1];
            var ftpCmds = cmdArgs[2].Split(';');
            var username = this.CurrentCreds.GetUsername(credKey);
            var password = this.CurrentCreds.GetPassword(credKey);
            Log.Trace("Beginning Ftp to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {

                try
                {
                    NetworkCredential netcred = new NetworkCredential(username, password);
                    foreach (var cmd in ftpCmds)
                    {
                        try
                        {
                            this.CurrentFtpSupport.RunFtpCommand(hostIp, netcred, cmd.Trim());
                            if (this.CurrentFtpSupport.TimeBetweenCommandsMin != 0 && this.CurrentFtpSupport.TimeBetweenCommandsMax != 0 && this.CurrentFtpSupport.TimeBetweenCommandsMin < this.CurrentFtpSupport.TimeBetweenCommandsMax)
                            {
                                Thread.Sleep(_random.Next(this.CurrentFtpSupport.TimeBetweenCommandsMin, this.CurrentFtpSupport.TimeBetweenCommandsMax));
                            }
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
                        }
                        catch (Exception e)
                        {
                            Log.Error(e); //some error occurred during this command, try the next one
                        }
                    }

                }
                catch (ThreadAbortException)
                {
                    throw;  //pass up
                }
                catch (Exception e)
                {
                    Log.Error(e);
                    return;  //unable to connect
                }

            }  

        }
    }
}
