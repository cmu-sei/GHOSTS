using System;
using System.IO;
using System.Threading;
using ghosts.client.linux.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using Newtonsoft.Json;
using Renci.SshNet;

/*
 * Used Package Renci.sshNet
 * Installed via packag manager
 * Install-Package SSH.NET
 */

namespace ghosts.client.linux.handlers
{
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
    public class Ssh : BaseHandler
    {
        private readonly Credentials CurrentCreds = null;
        private readonly SshSupport CurrentSshSupport = null;   //current SshSupport for this object
        public int jitterfactor = 0;

        public Ssh(TimelineHandler handler)
        {
            try
            {
                Init(handler);
                CurrentSshSupport = new SshSupport();
                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.TryGetValue("CredentialsFile", out var v1))
                    {
                        try
                        {
                            CurrentCreds = JsonConvert.DeserializeObject<Credentials>(File.ReadAllText(v1.ToString()));
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("ValidExts", out var v2))
                    {
                        try
                        {
                            CurrentSshSupport.ValidExts = v2.ToString().Split(';');
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("CommandTimeout", out var v3))
                    {
                        try
                        {
                            CurrentSshSupport.CommandTimeout = int.Parse(v3.ToString());
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMax", out var v4))
                    {
                        try
                        {
                            CurrentSshSupport.TimeBetweenCommandsMax = int.Parse(v4.ToString());
                            if (CurrentSshSupport.TimeBetweenCommandsMax < 0) CurrentSshSupport.TimeBetweenCommandsMax = 0;
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("TimeBetweenCommandsMin", out var v5))
                    {
                        try
                        {
                            CurrentSshSupport.TimeBetweenCommandsMin = int.Parse(v5.ToString());
                            if (CurrentSshSupport.TimeBetweenCommandsMin < 0) CurrentSshSupport.TimeBetweenCommandsMin = 0;
                        }
                        catch (Exception e)
                        {
                            _log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.TryGetValue("delay-jitter", out var v6))
                    {
                        jitterfactor = Jitter.JitterFactorParse(v6.ToString());
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
                _log.Trace("Ssh closing...");
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

                _log.Trace($"SSH Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");

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
            var credKey = cmdArgs[1];
            var sshCmds = cmdArgs[2].Split(';');
            var username = CurrentCreds.GetUsername(credKey);
            var password = CurrentCreds.GetPassword(credKey);
            _log.Trace("Beginning SSH to host:  " + hostIp + " with command: " + command);

            if (username != null && password != null)
            {

                //have IP, user/pass, try connecting
                using (var client = new SshClient(hostIp, username, password))
                {
                    try
                    {
                        client.Connect();
                    }
                    catch (ThreadAbortException)
                    {
                        throw;  //pass up
                    }
                    catch (Exception e)
                    {
                        _log.Error(e);
                        return;  //unable to connect
                    }
                    //we are connected, execute the commands
                    var shellStreamSSH = client.CreateShellStream("vt220", 80, 60, 800, 600, 65536);
                    //before running commands, flush the input of welcome login text
                    CurrentSshSupport.GetSshCommandOutput(shellStreamSSH, true);
                    foreach (var sshCmd in sshCmds)
                    {
                        try
                        {
                            CurrentSshSupport.RunSshCommand(shellStreamSSH, sshCmd.Trim());
                        }
                        catch (ThreadAbortException)
                        {
                            throw;  //pass up
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
