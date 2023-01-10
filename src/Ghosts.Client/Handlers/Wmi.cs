using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Renci.SshNet;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using Ghosts.Domain.Code;
using WorkingHours = Ghosts.Client.Infrastructure.WorkingHours;

namespace Ghosts.Client.Handlers
{
    public class Wmi : BaseHandler
    {

        private Credentials CurrentCreds = null;
        private WmiSupport CurrentWmiSupport = null;   //current WmiSupport for this object
        public int jitterfactor = 0;


        public Wmi(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                this.CurrentWmiSupport = new WmiSupport();
                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.ContainsKey("CredentialsFile"))
                    {
                        try
                        {
                            this.CurrentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(handler.HandlerArgs["CredentialsFile"].ToString()));
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
                            this.CurrentWmiSupport.TimeBetweenCommandsMax = Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMax"].ToString());
                            if (this.CurrentWmiSupport.TimeBetweenCommandsMax < 0) this.CurrentWmiSupport.TimeBetweenCommandsMax = 0;
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
                            this.CurrentWmiSupport.TimeBetweenCommandsMin = Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMin"].ToString());
                            if (this.CurrentWmiSupport.TimeBetweenCommandsMin < 0) this.CurrentWmiSupport.TimeBetweenCommandsMin = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
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
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Command);
                Log.Trace("Wmi closing...");
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

                Log.Trace($"Wmi Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

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
            var credKey = cmdArgs[1];
            var WmiCmds = cmdArgs[2].Split(';');
            var username = this.CurrentCreds.GetUsername(credKey);
            var password = this.CurrentCreds.GetPassword(credKey);
            Log.Trace("Beginning Wmi to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {

                //have IP, user/pass, try connecting 
                this.CurrentWmiSupport.Init(hostIp, username, password);
                this.CurrentWmiSupport.HostIp = hostIp; //for trace output
                var client = this.CurrentWmiSupport;
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
                    
                    
                    foreach (var WmiCmd in WmiCmds)
                    {
                        try
                        {
                            this.CurrentWmiSupport.RunWmiCommand(WmiCmd.Trim());
                            if (this.CurrentWmiSupport.TimeBetweenCommandsMin != 0 && this.CurrentWmiSupport.TimeBetweenCommandsMax != 0 && this.CurrentWmiSupport.TimeBetweenCommandsMin < this.CurrentWmiSupport.TimeBetweenCommandsMax)
                            {
                                Thread.Sleep(_random.Next(this.CurrentWmiSupport.TimeBetweenCommandsMin, this.CurrentWmiSupport.TimeBetweenCommandsMax));
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e); //some error occurred during this command, try the next one
                        }
                    }
                    this.Report(handler.HandlerType.ToString(), command, "", timelineEvent.TrackableId);
                }
            }

        }


    }
}
