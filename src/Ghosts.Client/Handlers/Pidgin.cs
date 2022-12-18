using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using Ghosts.Domain.Code;
using WorkingHours = Ghosts.Client.Infrastructure.WorkingHours;
using System.Net.Sockets;
using NLog.Layouts;
using NPOI.HSSF.Record.Common;
using System.Runtime.InteropServices;
using AutoItX3Lib;
using System;
using Newtonsoft.Json.Linq;
using Microsoft.Office.Interop.Word;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Security.Permissions;
using Microsoft.Office.Interop.Outlook;
using Exception = System.Exception;
using HtmlAgilityPack;
using NPOI.SS.Formula.Functions;
using Ghosts.Client.Infrastructure.Browser;

namespace Ghosts.Client.Handlers
{

    /// <summary>
    /// Exercises a Pidgin client - tested with Pidgin 2.14.1 (libpurple 2.14.1)
    /// It assumed that Pidgin is already configured with an enabled account in %APPDATA%\.purple\accounts.xml 
    /// and pointing to the target server. 
    /// It  is assumed that the logged in user only has one Pidgin account, and that this is enabled when pidgin is opened
    /// Also assumed that preferences are set in  %APPDATA%\.purple\prefs.xml
    /// Chat logging must enabled to the default directory
    /// The time line lists chat targets (username@domain)
    /// At startup, Pidgin is started if it is not up.
    /// An FileSystemWatcher is setup for %APPDATA%/Roaming/.purple/logs
    /// Any new directory/file creation/file write is monitored.
    /// The file watcher events are saved on a local queue that is handled during an activation period
    /// The queue is simply a list of file names, queue capacity is fixed.
    /// At startup, the queue is initialized with the names of the most recent files %APPDATA%/.purple/logs 
    /// whose path contains the UserAccount name, ie,%APPDATA%\.purple\logs\(protocol)\(useraccount)\(chattarget)
    /// The last directory element is the chat target, so jabber\susan@sitea.com\bill@sitea.com is susan is local account, bill is chat target
    /// For each activation period, the following is done:
    ///   1. if the Pidgin 'Buddy List' window is not detected, then Pidgin is started if it is not already started.
    ///   2. Search for open IM chat windows. For each one found, execute a response probability and respond.
    ///   3. If there are no open chat windows, pick one of the targets from the timline, open an IM chat, and start a chat.
    ///   4. If there are no open chat windows, and the timline is empty, then nothing will be done until an incoming chat is recieved.
    ///   
    /// When responding to a chat, we have the chat window title as user@domain.
    /// Foreach chat window title, find the corresponding chat log by looking through the queue for a file name
    /// that contains useraccount and whose chat target matches the window title.
    /// Once the file is located, parse the chat log find the last entry.
    /// If the last entry was sent by the local user account, we are waiting for reply. If the 
    /// wait time for a reply has been exceeded, then close the IM window.
    /// If the last entry was sent by the chat target, then respond, and record the reply time.
    /// 
    /// 
    /// </summary>
    public class Pidgin : BaseHandler
    {
        private int TimeBetweenMessagesMax  = 10000;
        private int TimeBetweenMessagesMin  = 4000;
        private int RepliesMin = 0;
        private int RepliesMax = 6;
        private int EmojiProbability = 10;
        private int NewMessageProbability = 60;
        private int jitterfactor = 50;
        private string UserAccount = null;   //pidgin user account
        private List<string> emojis;
        private string exepath = "C:\\Program Files (x86)\\Pidgin\\pidgin.exe";
        private ChatContent messages;


        public Pidgin(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                messages = new ChatContent();
                emojis = new List<string>()
                {
                    ":beer:",":coffe:",":money:",":moon:",";star:",":|","\\m/",
                    ":-D",":-(",";-)",":P", "=-O", ":kiss:","8-)",":-[",":'-(",":-/",
                    "O:-)",":-X",":-$",":-!",">:o",">:-(",":yes:",":no:",
                    ":wait:","@->--",":telephone:",":email:",":jabber:",":cake:",":heart:",":brokenheart:",":music:"

                };

                if (handler.HandlerArgs != null)
                {

                    
                    if (handler.HandlerArgs.ContainsKey("TimeBetweenMessagesMax"))
                    {
                        try
                        {
                            this.TimeBetweenMessagesMax = Int32.Parse(handler.HandlerArgs["TimeBetweenMessagesMax"].ToString());
                            if (this.TimeBetweenMessagesMax < 0) this.TimeBetweenMessagesMax = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("TimeBetweenMessagesMin"))
                    {
                        try
                        {
                            this.TimeBetweenMessagesMin = Int32.Parse(handler.HandlerArgs["TimeBetweenMessagesMin"].ToString());
                            if (this.TimeBetweenMessagesMin < 0) this.TimeBetweenMessagesMin = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("RepliesMin"))
                    {
                        try
                        {
                            this.RepliesMin = Int32.Parse(handler.HandlerArgs["RepliesMin"].ToString());
                            if (this.RepliesMin < 0) this.RepliesMin = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("RepliesMax"))
                    {
                        try
                        {
                            this.RepliesMax = Int32.Parse(handler.HandlerArgs["RepliesMax"].ToString());
                            if (this.RepliesMax < 0) this.RepliesMax = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("EmojiProbability"))
                    {
                        try
                        {
                            this.EmojiProbability = Int32.Parse(handler.HandlerArgs["EmojiProbability"].ToString());
                            if (this.EmojiProbability < 0 ) this.EmojiProbability = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("NewMessageProbability"))
                    {
                        try
                        {
                            this.NewMessageProbability = Int32.Parse(handler.HandlerArgs["NewMessageProbability"].ToString());
                            if (this.NewMessageProbability < 0) this.NewMessageProbability = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }

                    if (handler.HandlerArgs.ContainsKey("UserAccount"))
                    {
                        this.UserAccount = handler.HandlerArgs["UserAccount"].ToString();
                    }




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
                        break;  //if this ever returns, break
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
                Log.Trace("Pidgin closing...");
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

                Log.Trace($"Pidgin Command: {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

                switch (timelineEvent.Command)
                {
                    case "random":
                        while (true)
                        {
                            var cmd = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(cmd.ToString()))
                            {
                                if (!this.Command(handler, timelineEvent, cmd.ToString())) return;
                                
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor)); ;
                        }
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor)); ;
            }
        }

        public bool Command(TimelineHandler handler, TimelineEvent timelineEvent, string command)
        {
            var chatTarget = command;

            Log.Trace($"Beginning Pidgin activity cycle ");

            try
            {
                IntPtr pidginHandle = getBuddyListWindow();
                if (pidginHandle == IntPtr.Zero)
                {
                    Log.Trace($"Pidgin:: Unable to start Pidgin, exiting handler. ");
                    return false;
                }

                
                AutoItX3 au = new AutoItX3();

                //determine if there are any open IM windows
                string imWindowTitle = getImWindow();
                if (imWindowTitle == null)
                {
                    if (NewMessageProbability < _random.Next(0, 100)) return true; //skip this cycle
                    openChatWindow(au, chatTarget);
                }

                int numReplies = _random.Next(RepliesMin, RepliesMax);
                int i = 0;
               while ( i < numReplies)
                {
                    imWindowTitle = getImWindow();
                    if (imWindowTitle == null) break;
                    string currentImWindowTitle;
                    List<string> targetList = new List<string>();
                    var thisTarget = getChatTargetFromWindowTitle(imWindowTitle);
                    targetList.Add(getChatTargetFromWindowTitle(imWindowTitle));

                    //need to respond to each one
                    while (true)
                    {
                        sendChatMessage(au, imWindowTitle, thisTarget);
                        currentImWindowTitle = selectNextChat(au, imWindowTitle);
                        i++;
                        if (i >= numReplies) break;
                        thisTarget = getChatTargetFromWindowTitle(currentImWindowTitle);
                        if (currentImWindowTitle == null || targetList.Contains(thisTarget)) break;
                        //this is a new chat
                        imWindowTitle = currentImWindowTitle;
                        targetList.Add(thisTarget);
                        Thread.Sleep(_random.Next(TimeBetweenMessagesMin, TimeBetweenMessagesMax));
                        
                    }
                    
                    
                }
                return true;
                
            }

            catch (Exception e)
            {


                Log.Error(e);

            }
            return true;
           
        }

        
        private string getChatTargetFromWindowTitle(string windowTitle)
        {
            
            if (windowTitle != null && windowTitle.Contains("/"))
            {
                char[] seps = { '/' };
                return windowTitle.Split(seps)[0];
            }
            else
            {
                return windowTitle;
            }
        }

       
        /// <summary>
        /// Open a new chat window to the imTarget
        /// </summary>
        /// <param name="imTarget"></param>
        private void openChatWindow(AutoItX3 au, string imTarget)
        {
            var pidginHandle = Winuser.FindWindow("gdkWindowToplevel", "Buddy List");
            if (pidginHandle != IntPtr.Zero)
            {
                Winuser.SetForegroundWindow(pidginHandle);
                System.Windows.Forms.SendKeys.SendWait("^m");
                Thread.Sleep(1000);
                pidginHandle = Winuser.FindWindow("gdkWindowToplevel", "Pidgin");
                Winuser.SetForegroundWindow(pidginHandle);
                AutoItX3 au3 = new AutoItX3();
                var windHandle = au3.WinGetHandle("Pidgin");
                au3.WinActivate(windHandle);
                au3.Send(imTarget);
                au3.Send("{TAB}");
                au3.Send("{TAB}");
                au3.Send("{ENTER}");
                Thread.Sleep(500);

            }
        }

        /// <summary>
        /// Select the next tab in the IM chat window
        /// </summary>
        /// <param name="au"></param>
        /// <param name="windowTitle"></param>
        /// <returns></returns>
        private string selectNextChat(AutoItX3 au, string windowTitle)
        {
            var windHandle = Winuser.FindWindow("gdkWindowToplevel", windowTitle);
            Winuser.SetForegroundWindow(windHandle);
            au.Send("^{TAB}");
            Thread.Sleep(500);
            //at this point the window title may have changed. Check that.
            return getImWindow(); //return the new title of the IM window

        }

        /// <summary>
        /// Send a chat message to the current target in the IM chat window
        /// </summary>
        /// <param name="au"></param>
        /// <param name="windowTitle"></param>
        private void sendChatMessage(AutoItX3 au, string windowTitle, string chatTarget)
        {
            try
            {
                var windHandle = Winuser.FindWindow("gdkWindowToplevel", windowTitle);
                Winuser.SetForegroundWindow(windHandle);
                var msg = messages.MessageNext();
                if (EmojiProbability > _random.Next(0, 100))
                {
                    var numEmojis = _random.Next(1, 5);
                    for(int i = 0; i < numEmojis; i++)
                    {
                        msg = msg + emojis[_random.Next(0,emojis.Count)];
                    }
                }
                au.Send(msg);
                au.Send("{ENTER}");
                Thread.Sleep(1000);
                Log.Trace($"Pidgin:: Sent message to target {chatTarget}. ");
            }
            catch (Exception e)
            {

                Log.Error(e);
                //this.Report(handler.HandlerType.ToString(), command, "", timelineEvent.TrackableId);

            }
        }

        /// <summary>
        /// There is only one IM window, find and return the current title.
        /// The title reflects the currently selected chat
        /// </summary>
        private string getImWindow()
        {
           
            var plist = Process.GetProcessesByName("Pidgin");
            foreach (var process in plist)
            {
                if (process.MainWindowTitle.Contains("@"))
                {
                    //assume this is the IM chat window
                    return process.MainWindowTitle;
                }
            }
            return null;

        }

        /// <summary>
        /// This looks for the buddy list window. If not found, start Pidgin, then look for it
        /// </summary>
        /// <returns></returns>
        private IntPtr getBuddyListWindow()
        {
            var plist = Process.GetProcessesByName("Pidgin");

            if (plist.Length == 0)
            {
                //try to start Pidgin
                Process.Start(exepath);
                Thread.Sleep(1000);
            }

            else if (plist.Length > 0)
            {
                var proc = plist[0];
                if (proc.MainWindowTitle == "")
                {
                    //this is the background process. Start pidgin
                    //try to start Pidgin
                    Process.Start(exepath);
                    Thread.Sleep(1000);
                }
                    
            }
            return Winuser.FindWindow("gdkWindowToplevel", "Buddy List");
        }

          

    }


 }
