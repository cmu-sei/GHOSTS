using System;
using System.IO;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Newtonsoft.Json;
using Ghosts.Domain.Code;
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
                int value;
                if (handler.HandlerArgs.ContainsKey("deletion-probability"))
                {
                    int.TryParse(handler.HandlerArgs["deletion-probability"].ToString(), out value);
                    this.CurrentFtpSupport.deletionProbability = value;
                    if (!CheckProbabilityVar(handler.HandlerArgs["deletion-probability"].ToString(), this.CurrentFtpSupport.deletionProbability))
                    {
                        this.CurrentFtpSupport.deletionProbability = 20;
                    }
                }
                if (handler.HandlerArgs.ContainsKey("download-probability"))
                {
                    int.TryParse(handler.HandlerArgs["download-probability"].ToString(), out value);
                    this.CurrentFtpSupport.downloadProbability = value;
                    if (!CheckProbabilityVar(handler.HandlerArgs["download-probability"].ToString(), this.CurrentFtpSupport.downloadProbability))
                    {
                        this.CurrentFtpSupport.downloadProbability = 40;
                    }
                }
                if (handler.HandlerArgs.ContainsKey("upload-probability"))
                {
                    int.TryParse(handler.HandlerArgs["upload-probability"].ToString(), out value);
                    this.CurrentFtpSupport.uploadProbability = value;
                    if (!CheckProbabilityVar(handler.HandlerArgs["upload-probability"].ToString(), this.CurrentFtpSupport.uploadProbability))
                    {
                        this.CurrentFtpSupport.uploadProbability = 40;
                    }
                }
                if ((this.CurrentFtpSupport.deletionProbability + this.CurrentFtpSupport.uploadProbability + this.CurrentFtpSupport.downloadProbability) > 100)
                {
                    Log.Trace("Ftp:: Sum of deletion/upload/download probabilities > 100, setting to defaults.");
                    this.CurrentFtpSupport.uploadProbability = 40;
                    this.CurrentFtpSupport.downloadProbability = 40;
                    this.CurrentFtpSupport.deletionProbability = 20;
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

                if (timelineEvent.DelayBeforeActual > 0)
                    Thread.Sleep(timelineEvent.DelayBeforeActual);

                Log.Trace($"Ftp Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");
                int[] probabilityList = { this.CurrentFtpSupport.uploadProbability, this.CurrentFtpSupport.downloadProbability, this.CurrentFtpSupport.deletionProbability };
                string[] actionList = { "upload", "download", "delete" };

                switch (timelineEvent.Command)
                {
                    case "random":
                        var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                        if (!string.IsNullOrEmpty(cmd.ToString()))
                        {
                            var action = SelectActionFromProbabilities(probabilityList, actionList);
                            this.Command(handler, timelineEvent, cmd.ToString(), action);
                        }
                        Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
                        break;    
                }

                if (timelineEvent.DelayAfterActual > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor)); ;
            }
        }


        public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command, string action)
        {
            
            char[] charSeparators = new char[] { '|' };
            var cmdArgs = command.Split(charSeparators, 2, StringSplitOptions.None);
            var hostIp = cmdArgs[0];
            this.CurrentFtpSupport.HostIp = hostIp; //for trace output
            var credKey = cmdArgs[1];
            var username = this.CurrentCreds.GetUsername(credKey);
            var password = this.CurrentCreds.GetPassword(credKey);
            Log.Trace("Beginning Ftp to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {

                try
                {
                    NetworkCredential netcred = new NetworkCredential(username, password);
                   
                    try
                    {
                        this.CurrentFtpSupport.RunFtpCommand(hostIp, netcred, action);
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch (Exception e)
                    {
                        Log.Error(e); //some error occurred during this command, try the next one
                    }
                    Report(new ReportItem { Handler = handler.HandlerType.ToString(), Command = hostIp, Arg = action, Trackable = timelineEvent.TrackableId });

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
