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
    /// Exercises a Pidgin client - tested with Pidgin 2.14.1 (libpurple 2.14.1) ane Centos 7.3 ejabberd server
    /// Prequisites
    ///   Pidgin must be installed and already configured with an enabled account in %APPDATA%\.purple\accounts.xml 
    ///     and pointing to the target server. 
    ///   
    ///   The logged in user must have an enabled Pidgin account in accounts.xml
    ///   Pidgin preferences must have already been set in  %APPDATA%\.purple\prefs.xml
    ///   Conversations must be TABBED (in prefs.xml/conversations section, name='tabs' type='bool' value='1')
    ///   This implementation uses AutoItX and the GHOSTSINSTALL\AutoItX3.dll must be registered.
    ///     To register the DLL, execute with admin priviledges: C:\Windows\System32\regsvr32.exe  targetdll   during the client setup.
    /// Implementation
    ///   This implementation is about 95% open loop as there are no C# bindings for the Pidgin libpurple.dll
    ///   The only feedback to GHOSTS is via window titles, it cannot determine when messages arrive or message content.
    ///   GHOSTS cannot parse the chat logs to synch converstations as the Pidgin process has these log files locked.
    ///   So messages are sent open loop with simple delays between messages.
    ///   The GHOSTS time line CommandArgs lists chat targets (username@domain)
    /// Activity Cycle - each activity cycle is seperated by DelayAfter. An activity cycle does:
    ///   Pick a random target from the timeline -  this is only used to initiate the first chat
    ///   If Pidgin is not started then Pidgin is started.
    ///   If an IM window is not open, the roll against NewChatProbability and open an IM window to the random target chosen from the timeline
    ///   If roll against  NewChatProbability was not successful, end activity cycle.
    ///   If an IM window is open and a new chat was not initiated, the roll against CloseChatProbability, if successful, close current chat and end activity cycle.
    ///   If get to this point, then IM window is open with one or more targets and message loop is entered.
    ///   Enter a loop in which between RepliesMin and RepliesMax messages are sent.
    ///   The first message is sent to current selected target in the Chat window, then the next chat 
    ///   target in the Chat window is selected. If the max replies is reached, then the loop exits and
    ///   the activity cycle is ended. The next activity cycle picks up where the last activity cycle 
    ///   ended as per the first chat target.
    /// 
    ///   
    ///   A chat target can be the current logged in user, which means messages are simply echoed back from the server.
    ///   As chats arrive from other different users, the number of open tabs in the grows, but chats can be closed by CloseChatProbability
    ///   Between 1-4 random emojis are added to a message based on EmojiProbability
    ///   
    ///   During an activity cycle, any popup windows that match a title in ErrorWindowTitles are closed
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
        private int NewChatProbability = 60;
        private int CloseChatProbability = 10;
        private int jitterfactor = 50;
        private List<string> emojis;
        private string Exepath = "C:\\Program Files (x86)\\Pidgin\\pidgin.exe";
        private List<string> ErrorWindowTitles;   //list of window titles for possible popup error windows that should be closed
        private List<string> MiscWindowTitles;  //list of window titles that sometime popup and must be closed
        private ChatContent messages;  


        public Pidgin(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
                messages = new ChatContent();
                //default error window titles, can be protocol specific
                ErrorWindowTitles = new List<string>()
                {
                    "XMPP Message Error"
                };
                MiscWindowTitles = new List<string>()
                {
                    "Accounts", "Modify Account"
                };

                emojis = new List<string>()
                {
                    ":beer:",":coffee:",":money:",":moon:",":star:",":|","\\m/",
                    ":-D",":-(",";-)",":P", "=-O", ":kiss:","8-)",":-[",":'-(",":-/",
                    "O:-)",":-X",":-$",":-!",">:o",">:-(",":yes:",":no:",
                    ":wait:","@->--",":telephone:",":email:",":jabber:",":cake:",":heart:",":brokenheart:",":music:"

                };

                if (handler.HandlerArgs != null)
                {
                    if (handler.HandlerArgs.ContainsKey("ErrorWindowTitles"))
                    {
                        try
                        {
                            ErrorWindowTitles = new List<string>();
                            foreach (var option in (JArray)handler.HandlerArgs["ErrorWindowTitles"])
                            {
                                ErrorWindowTitles.Add(option.Value<string>());
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }

                    if (handler.HandlerArgs.ContainsKey("Exepath"))
                    {
                        try
                        {
                            string targetExe = handler.HandlerArgs["Exepath"].ToString();
                            targetExe = Environment.ExpandEnvironmentVariables(targetExe);
                            if (!File.Exists(targetExe))
                            {
                                Log.Trace($"Pidgin:: Exepath {targetExe} does not exist, using default of {Exepath}.");
                            }
                            else
                            {
                                Exepath = targetExe;
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }

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
                    if (handler.HandlerArgs.ContainsKey("CloseChatProbability"))
                    {
                        try
                        {
                            this.CloseChatProbability = Int32.Parse(handler.HandlerArgs["CloseChatProbability"].ToString());
                            if (this.CloseChatProbability < 0) this.CloseChatProbability = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
                    }
                    if (handler.HandlerArgs.ContainsKey("NewChatProbability"))
                    {
                        try
                        {
                            this.NewChatProbability = Int32.Parse(handler.HandlerArgs["NewChatProbability"].ToString());
                            if (this.NewChatProbability < 0) this.NewChatProbability = 0;
                        }
                        catch (Exception e)
                        {
                            Log.Error(e);
                        }
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

                Log.Trace($"Pidgin::  Command {timelineEvent.Command} with delay after of {timelineEvent.DelayAfter}");

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
                closeWindows(MiscWindowTitles);
                closeWindows(ErrorWindowTitles);
                IntPtr pidginHandle = getBuddyListWindow();
                if (pidginHandle == IntPtr.Zero)
                {
                    Log.Trace($"Pidgin:: Unable to start Pidgin, exiting handler. ");
                    return false;
                }

               
                AutoItX3 au = new AutoItX3();
                bool chatInitiated = false;

                //determine if there are any open IM windows
                string imWindowTitle = getImWindow();
                if (imWindowTitle == null)
                {
                    if (NewChatProbability < _random.Next(0, 100)) return true; //skip this cycle
                    openChatWindow(au, chatTarget);
                    chatInitiated = true;
                }
                if (!chatInitiated && (_random.Next(0, 100) <= CloseChatProbability)) {
                    //close the currently selected chat
                    imWindowTitle = getImWindow();
                    if (imWindowTitle == null) return true;
                    closeCurrentlySelectedChat(imWindowTitle);
                    return true;
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
                        Thread.Sleep(_random.Next(TimeBetweenMessagesMin, TimeBetweenMessagesMax));
                        currentImWindowTitle = selectNextChat(au, imWindowTitle);
                        i++;
                        if (i >= numReplies) break;
                        thisTarget = getChatTargetFromWindowTitle(currentImWindowTitle);
                        if (currentImWindowTitle == null || targetList.Contains(thisTarget)) break;
                        //this is a new chat
                        imWindowTitle = currentImWindowTitle;
                        targetList.Add(thisTarget);
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
        /// Close any error windows due to chat sending
        /// </summary>
        private void closeWindows(List<string> windowTitles)
        {
            foreach (var windowTitle in windowTitles)
            {
                while (true)
                {
                    var winHandle = Winuser.FindWindow("gdkWindowToplevel", windowTitle);
                    if (winHandle == IntPtr.Zero) break;
                    Winuser.SetForegroundWindow(winHandle);
                    System.Windows.Forms.SendKeys.SendWait("%c");  //close the window
                    Thread.Sleep(1000);
                    Log.Trace($"Pidgin:: Closed window {windowTitle}. ");
                }
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
            var windHandle = Winuser.FindWindow("gdkWindowToplevel", "Pidgin");
            Winuser.SetForegroundWindow(windHandle);
            au.Send("^{TAB}");
            Thread.Sleep(500);
            //at this point the window title may have changed. Check that.
            return getImWindow(); //return the new title of the IM window

        }

        private void closeCurrentlySelectedChat( string windowTitle)
        {
            
            var windHandle = Winuser.FindWindow("gdkWindowToplevel", windowTitle);
            Winuser.SetForegroundWindow(windHandle);
            System.Windows.Forms.SendKeys.SendWait("^w");
            Log.Trace($"Pidgin:: closed chat {windowTitle}. ");
            Thread.Sleep(500);
            

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
                msg = msg + "{ENTER}";
                au.Send(msg);
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
        /// This looks for the buddy list window. If not found, start Pidgin, then look for itf bet friends with like all celebrities, so. . . .
        /// 
        /// </summary>
        /// <returns></returns>
        private IntPtr getBuddyListWindow()
        {
            var plist = Process.GetProcessesByName("Pidgin");

            if (plist.Length == 0)
            {
                //try to start Pidgin
                Log.Trace($"Pidgin:: Starting Pidgin. ");
                Process.Start(Exepath);
                Thread.Sleep(180000);   //wait for three minutes for pidgin to start and make contact with server 
            }

            else if (plist.Length > 0)
            {
                var proc = plist[0];
                if (proc.MainWindowTitle == "")
                {
                    //this is the background process. Start pidgin
                    //try to start Pidgin
                    Log.Trace($"Pidgin:: Starting Pidgin. ");
                    Process.Start(Exepath);
                    Thread.Sleep(180000);  //wait for three minutes for pidgin to start and make contact with server 
                }
                    
            }
            return Winuser.FindWindow("gdkWindowToplevel", "Buddy List");
        }

          

    }


 }
