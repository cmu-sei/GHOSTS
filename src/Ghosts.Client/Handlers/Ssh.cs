using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Renci.SshNet.Connection;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;

/*
 * Used Package Renci.sshNet
 * Installed via packag manager
 * Install-Package SSH.NET
 * 
 */

namespace Ghosts.Client.Handlers
{
    public class Ssh : BaseHandler
    {

        private Credentials currentCreds = null;
        
        public Ssh(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                if (handler.HandlerArgs != null && handler.HandlerArgs.ContainsKey("credfile"))
                {
                    try
                    {
                        this.currentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(File.ReadAllText(handler.HandlerArgs["credfile"].ToString())));
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
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
                Log.Trace("Cmd closing...");
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

                Log.Trace($"Command line: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

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
                            Thread.Sleep(timelineEvent.DelayAfter);
                        }
                    default:
                        this.Command(handler, timelineEvent, timelineEvent.Command);

                        foreach (var cmd in timelineEvent.CommandArgs)
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                                this.Command(handler, timelineEvent, cmd.ToString());
                        break;
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(timelineEvent.DelayAfter);
            }
        }

        public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
        {
            Log.Trace("Spawning cmd.exe...");
            var processStartInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            using (var process = Process.Start(processStartInfo))
            {
                Thread.Sleep(1000);
                if (process != null)
                {
                    process.StandardInput.WriteLine(command);
                    process.StandardInput.Close(); // line added to stop process from hanging on ReadToEnd()
                    var outputString = process.StandardOutput.ReadToEnd();
                    this.Report(handler.HandlerType.ToString(), command, outputString, timelineEvent.TrackableId);
                }
            }
        }



    }

}
