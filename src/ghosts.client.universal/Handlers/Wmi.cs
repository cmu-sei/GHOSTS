// Copyright 2017 Carnegie Mellon University. All Rights Reserved. See LICENSE.md file for terms.

using System;
using System.Threading;
using System.Threading.Tasks;
using Ghosts.Domain;

namespace Ghosts.Client.Universal.Handlers;

public class Wmi(Timeline entireTimeline, TimelineHandler timelineHandler, CancellationToken cancellationToken)
    : BaseHandler(entireTimeline, timelineHandler, cancellationToken)
{
    protected override Task RunOnce()
    {
        throw new NotImplementedException();
    }

    // private Credentials CurrentCreds = null;
    // private WmiSupport CurrentWmiSupport = null; //current WmiSupport for this object
    // public int jitterfactor = 0;
    //
    //
    // public Wmi(TimelineHandler handler)
    // {
    //     try
    //     {
    //         base.Init(handler);
    //         this.CurrentWmiSupport = new WmiSupport();
    //         if (handler.HandlerArgs != null)
    //         {
    //             if (handler.HandlerArgs.ContainsKey("CredentialsFile"))
    //             {
    //                 try
    //                 {
    //                     this.CurrentCreds =
    //                         JsonConvert.DeserializeObject<Credentials>(
    //                             File.ReadAllText(handler.HandlerArgs["CredentialsFile"].ToString()));
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     _log.Error(e);
    //                 }
    //             }
    //
    //             if (handler.HandlerArgs.ContainsKey("Credentials"))
    //             {
    //                 try
    //                 {
    //                     this.CurrentCreds =
    //                         JsonConvert.DeserializeObject<Credentials>(
    //                             handler.HandlerArgs["Credentials"].ToString());
    //                 }
    //                 catch (ThreadAbortException)
    //                 {
    //                     throw; //pass up
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     _log.Error(e);
    //                 }
    //             }
    //
    //             if (handler.HandlerArgs.ContainsKey("TimeBetweenCommandsMax"))
    //             {
    //                 try
    //                 {
    //                     this.CurrentWmiSupport.TimeBetweenCommandsMax =
    //                         Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMax"].ToString());
    //                     if (this.CurrentWmiSupport.TimeBetweenCommandsMax < 0)
    //                         this.CurrentWmiSupport.TimeBetweenCommandsMax = 0;
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     _log.Error(e);
    //                 }
    //             }
    //
    //             if (handler.HandlerArgs.ContainsKey("TimeBetweenCommandsMin"))
    //             {
    //                 try
    //                 {
    //                     this.CurrentWmiSupport.TimeBetweenCommandsMin =
    //                         Int32.Parse(handler.HandlerArgs["TimeBetweenCommandsMin"].ToString());
    //                     if (this.CurrentWmiSupport.TimeBetweenCommandsMin < 0)
    //                         this.CurrentWmiSupport.TimeBetweenCommandsMin = 0;
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     _log.Error(e);
    //                 }
    //             }
    //         }
    //
    //         if (this.CurrentCreds == null)
    //         {
    //             _log.Error(
    //                 $"WMI:: No credentials supplied, either CredentialsFile or Credentials must be supplied in handler args, exiting.");
    //             return;
    //         }
    //
    //         if (handler.Loop)
    //         {
    //             while (true)
    //             {
    //                 Ex(handler);
    //             }
    //         }
    //         else
    //         {
    //             Ex(handler);
    //         }
    //     }
    //     catch (ThreadAbortException)
    //     {
    //         _log.Trace("Wmi closing...");
    //     }
    //     catch (Exception e)
    //     {
    //         _log.Error(e);
    //     }
    // }
    //
    // public void Ex(TimelineHandler handler)
    // {
    //     foreach (var timelineEvent in handler.TimeLineEvents)
    //     {
    //         WorkingHours.Is(handler);
    //
    //         if (timelineEvent.DelayBeforeActual > 0)
    //             Thread.Sleep(timelineEvent.DelayBeforeActual);
    //
    //         _log.Trace(
    //             $"Wmi Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfterActual}");
    //
    //         switch (timelineEvent.Command)
    //         {
    //             case "random":
    //                 var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
    //                 if (!string.IsNullOrEmpty(cmd.ToString()))
    //                 {
    //                     this.Command(handler, timelineEvent, cmd.ToString());
    //                 }
    //
    //                 Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
    //                 break;
    //         }
    //
    //         if (timelineEvent.DelayAfterActual > 0)
    //             Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, jitterfactor));
    //         ;
    //     }
    // }
    //
    //
    // public void Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
    // {
    //     char[] charSeparators = new char[] { '|' };
    //     var cmdArgs = command.Split(charSeparators, 3, StringSplitOptions.None);
    //     var hostIp = cmdArgs[0];
    //     var credKey = cmdArgs[1];
    //     var WmiCmds = cmdArgs[2].Split(';');
    //     var domain = this.CurrentCreds.GetDomain(credKey);
    //     var username = this.CurrentCreds.GetUsername(credKey);
    //     var password = this.CurrentCreds.GetPassword(credKey);
    //     _log.Trace("Beginning Wmi to host:  " + hostIp + " with command: " + command);
    //     if (domain == null)
    //     {
    //         domain = hostIp;
    //     }
    //
    //     if (username != null && password != null && domain != null)
    //     {
    //         //have IP, user/pass, try connecting
    //         this.CurrentWmiSupport.Init(hostIp, username, password, domain);
    //         this.CurrentWmiSupport.HostIp = hostIp; //for trace output
    //         var client = this.CurrentWmiSupport;
    //         {
    //             try
    //             {
    //                 client.Connect();
    //             }
    //             catch (ThreadAbortException)
    //             {
    //                 throw; //pass up
    //             }
    //             catch (Exception e)
    //             {
    //                 _log.Error(e);
    //                 return; //unable to connect
    //             }
    //             //we are connected, execute the commands
    //
    //
    //             foreach (var WmiCmd in WmiCmds)
    //             {
    //                 try
    //                 {
    //                     this.CurrentWmiSupport.RunWmiCommand(WmiCmd.Trim());
    //                     if (this.CurrentWmiSupport.TimeBetweenCommandsMin != 0 &&
    //                         this.CurrentWmiSupport.TimeBetweenCommandsMax != 0 &&
    //                         this.CurrentWmiSupport.TimeBetweenCommandsMin <
    //                         this.CurrentWmiSupport.TimeBetweenCommandsMax)
    //                     {
    //                         Thread.Sleep(_random.Next(this.CurrentWmiSupport.TimeBetweenCommandsMin,
    //                             this.CurrentWmiSupport.TimeBetweenCommandsMax));
    //                     }
    //                 }
    //                 catch (ThreadAbortException)
    //                 {
    //                     throw; //pass up
    //                 }
    //                 catch (Exception e)
    //                 {
    //                     _log.Error(e); //some error occurred during this command, try the next one
    //                 }
    //             }
    //
    //             client.Close();
    //             Report(new ReportItem
    //             {
    //                 Handler = handler.HandlerType.ToString(),
    //                 Command = hostIp,
    //                 Arg = cmdArgs[2],
    //                 Trackable = timelineEvent.TrackableId
    //             });
    //         }
    //     }
    // }
}
