using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using AutoItX3Lib;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using NPOI.SS.Formula.Functions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;

namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Remote Desktop Protocol Handler
    /// Action:  The logged-in user (i.e. ghosts user) opens a remote desktop to randomly chosen target, and does random
    /// mouse movements for the specified ExecutionTime. When this time has elapsed, the connection is closed,
    /// another target is chosen, and the cycle repeats.
    /// 
    /// The credential file is the same format as WMI, SFTP, SSH, etc.
    /// If only the password is specified, then the login user is assumed to be the current user.
    /// If username is specified (and optionally domain), and the current user is not the logged in user,
    /// then a login using the specified domain\username is used.
    /// 
    /// See the sample Rdp timeline for all options.
    /// 
    /// This handler will supply a password (and also usename if supplied) if prompted, and dismiss a untrused certificate check prompt if one
    /// appears.
    /// 
    /// It is assumed that the logged-in user (or the specified user) has the right to log into the target system.
    /// Caveat: This has only be tested in a Windows domain system where the domain policy was altered to allow all users
    /// remote desktop priviledges, and only tested using RDP to a domain controller server.
    /// It has also been tested using the Domain Admin user (not the current logged in user), in which the handler
    /// supplied both username and password to the crdential prompts. Using the Domain Admin user does not require
    /// modification of the domain policy to enable RDP.
    /// 
    /// </summary>
    public class Rdp : BaseHandler
    {
        private int MouseSleep = 10000;

        private Credentials CurrentCreds = null;

        private int ExecutionTime = 20000;

        private int ExecutionProbability = 100;
        private int JitterFactor = 0;  //used with Jitter.JitterFactorDelay

        private string CurrentTarget;

        private string CustomLogin = null;

        public Rdp(TimelineHandler handler)
        {
            try
            {
                base.Init(handler);
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
                Log.Trace("Rdp closing...");
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void Ex(TimelineHandler handler)
        {
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
                if (handler.HandlerArgs.ContainsKey("mouse-sleep-time"))
                {
                    int.TryParse(handler.HandlerArgs["mouse-sleep-time"].ToString(), out MouseSleep);
                    if (MouseSleep < 0) MouseSleep = 10000;
                }
                if (handler.HandlerArgs.ContainsKey("execution-time"))
                {
                    int.TryParse(handler.HandlerArgs["execution-time"].ToString(), out ExecutionTime);
                    if (ExecutionTime < 0) ExecutionTime = 5 * 60 * 1000;  //5 minutes
                }
                if (handler.HandlerArgs.ContainsKey("custom-login") &&
                    !string.IsNullOrEmpty(handler.HandlerArgs["custom-login"].ToString()))
                {
                    CustomLogin = handler.HandlerArgs["custom-login"].ToString();
                }

                if (handler.HandlerArgs.ContainsKey("execution-probability"))
                {
                    int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out ExecutionProbability);
                    if (ExecutionProbability < 0 || ExecutionProbability > 100) ExecutionProbability = 100;
                }
                if (handler.HandlerArgs.ContainsKey("delay-jitter"))
                {
                    JitterFactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
                }
            }
            AutoItX3 au = new AutoItX3();

            //arguments parsed. Enter loop that does not exit unless forced exit
            while (true)
            {

                foreach (var timelineEvent in handler.TimeLineEvents)
                {
                    WorkingHours.Is(handler);
                    switch (timelineEvent.Command)
                    {
                        case "random":
                        default:
                            if (ExecutionProbability < _random.Next(0, 100))
                            {
                                //skipping this command
                                Log.Trace($"RDP choice skipped due to execution probability");
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                                continue;
                            }
                            if (timelineEvent.DelayBeforeActual > 0)
                                Thread.Sleep(timelineEvent.DelayBeforeActual);
                            var choice = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(choice.ToString()))
                            {
                                this.RdpEx(handler, timelineEvent, choice.ToString(), au);
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfterActual, JitterFactor));
                            
                            break;

                    }
                }
            }
        }

        public void Cleanup(AutoItX3 au, string caption,Process process)
        {
            //close window if still open
            var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
            if (winHandle != IntPtr.Zero)
            {
                Log.Trace($"RDP:: Closing Remote Desktop window.");
                au.WinClose(caption);
                Thread.Sleep(1000);
                // handle the close dialog
                checkGenericPrompt(au, "{ENTER}");
                Thread.Sleep(1000);
            }
            if (process != null)
            {
                if (!process.HasExited)
                {
                    Log.Trace($"RDP:: Closing Remote Desktop process window.");
                    process.CloseMainWindow();
                    Thread.Sleep(10000);
                    if (!process.HasExited)
                    {
                        Log.Trace($"RDP:: Killing Remote Desktop process.");
                        process.Kill();
                        Thread.Sleep(1000);
                    }
                } else
                {
                    Log.Trace($"RDP:: Remote Desktop process has exited.");
                }
            }
            else
            {
                Log.Trace($"RDP:: No Remote Desktop process to cleanup.");
            }
        }

        public void RdpEx(TimelineHandler handler, TimelineEvent timelineEvent, string choice, AutoItX3 au)
        {

            char[] charSeparators = new char[] { '|' };
            var cmdArgs = choice.Split(charSeparators, 2, StringSplitOptions.None);
            var target = cmdArgs[0];
            CurrentTarget = target;
            string command = $"mstsc /v:{target}";
            var credKey = cmdArgs[1];
            var domain = CurrentCreds.GetDomain(credKey);
            var username = CurrentCreds.GetUsername(credKey);
            var password = CurrentCreds.GetPassword(credKey);
            Log.Trace($"RDP:: Spawning RDP connection for target {target}");
            var localUser = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            bool usePasswordOnly = true;
            if (username != null)
            {
                if (domain != null)
                {
                    username = $"{domain}\\{username}";
                }
                if (!localUser.ToLower().Contains(username.ToLower())) {
                    usePasswordOnly = false;
                }
            }

            var processStartInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            
            var caption = $"{target} - Remote Desktop Connection";

            using (var process = Process.Start(processStartInfo))
            {
                try
                {
                    Thread.Sleep(1000);
                    if (process != null)
                    {
                        process.StandardInput.WriteLine(command);
                        process.StandardInput.Close(); // line added to stop process from hanging on ReadToEnd()
                        Thread.Sleep(2000);  //give time for response
                        checkPasswordPrompt(au, password, username, usePasswordOnly);
                        //wait 15 seconds for connection window
                        if (!findRdpWindow(target, 15))
                        {
                            //may have a certificate problem
                            Log.Trace("RDP:: Unable to find a RDP window, attempting to accept an untrusted certificate");
                            checkGenericPrompt(au, "{TAB}{TAB}{TAB}{ENTER}");  //this is for a certificate prompt
                        }
                        //try again for another 4.25 minutes to find
                        if (findRdpWindow(target, 255))
                        {
                            doMouseLoop(caption, target, au, timelineEvent);
                        }
                        else
                        {
                            Log.Trace($"RDP:: Unable to connect to remote desktop for {target}");
                        }
                        Thread.Sleep(2000);
                        Cleanup(au, caption,process);
                    }
                    else
                    {
                        Log.Trace($"RDP:: Failed to execute the remote desktop connection program for {target}");
                    }
                }
                catch (ThreadAbortException)
                {
                    Cleanup(au, caption, process);
                    throw;  //pass up
                }
                catch (Exception e)
                {
                    Cleanup(au, caption, process);
                    Log.Error(e);
                }
            }
        }

        public bool findRdpWindow(string target,int timeout)
        {
            var caption = $"{target} - Remote Desktop Connection";
            var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
            int wait = 1; //time to wait for this loop (exponential back off: 2^n starting with n=0)
            //wait for window to appear 
            while (winHandle == IntPtr.Zero)
            {
                Log.Trace($"RDP:: Unable to find desktop window for {target}, sleeping {wait} seconds");
                Thread.Sleep(wait * 1000); //sleep for exponential back off amount of seconds
                if (2 * wait - 1 >= timeout) break; // sum(2^n) from n=0 -> n=x is 2 * 2^x - 1
                wait = 2 * wait; //double the time to wait next loop (2^n)
                winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
            }
            return winHandle != IntPtr.Zero;
        }

        public void doMouseLoop(string caption, string target, AutoItX3 au, TimelineEvent timelineEvent)
        {
            
            var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
            if (winHandle != IntPtr.Zero)
            {
                Log.Trace($"RDP:: Connected to {target}, beginning activity loop");
                // found the remote desktop window
                int totalTime = 0;
                while (totalTime < ExecutionTime)
                {
                    var sleepTime = Jitter.JitterFactorDelay(MouseSleep, JitterFactor);
                    Thread.Sleep(sleepTime);
                    totalTime += sleepTime;
                    //move the mouse
                    Winuser.SetForegroundWindow(winHandle);
                    Winuser.RECT rc;
                    Winuser.GetWindowRect(winHandle, out rc);
                    au.MouseMove(rc.Left, rc.Top, 30);
                    au.MouseMove(_random.Next(rc.Left, rc.Right), _random.Next(rc.Top, rc.Bottom), 30);
                    Winuser.SetForegroundWindow(winHandle);
                    Report(new ReportItem { Handler = "RDP", Command = CurrentTarget, Arg = "mouseloop", Trackable = timelineEvent.TrackableId });
                }
                //close the window
                Log.Trace($"RDP:: Finished activity loop for {target}.");
                au.WinClose(caption);
                Thread.Sleep(1000);
                // handle the close dialog
                checkGenericPrompt(au, "{ENTER}");
                Thread.Sleep(1000);

            }
            else
            {
                Log.Trace($"RDP:: Unable to find remote desktop window for {target}.");
            }
        }

        /// <summary>
        /// Escape special characters in string sent by sendkeys
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public static string escapePassword(string password)
        {
            char[] special = { '+', '^', '%', '(', ')', '~', '{', '}', '[', ']' };
            var s = password;
            foreach (var c in special)
            {

                var cstr = c.ToString();
                if (s.Contains(cstr))
                {
                    var rep = "{" + cstr + "}";
                    s = s.Replace(cstr, rep);
                }

            }
            return s;
        }

        public IntPtr findDialogWindow(string caption)
        {
            var winHandle = Winuser.FindWindow(null, caption);
            if (winHandle != IntPtr.Zero)
            {
                //this finds any window with this caption
                //determine if class name is for standard dialogue
                int bufSize = 256;
                StringBuilder buffer = new StringBuilder(bufSize);
                Winuser.GetClassName(winHandle, buffer, bufSize);
                var s = buffer.ToString();
                if (s.Contains("32770")) return winHandle; //success
            }
            return IntPtr.Zero;
        }

        public void checkGenericPrompt(AutoItX3 au, string closeString)
        {
            var caption = "Remote Desktop Connection";
            var winHandle = findDialogWindow(caption);
            if (winHandle != IntPtr.Zero)
            {
                //this is a standard dialog class. By default, chooses 'No'. Select yes prompt
                Winuser.SetForegroundWindow(winHandle);
                au.Send(closeString); //send the close string
                Thread.Sleep(1000);
                Log.Trace($"RDP:: Found window prompt, caption: {caption} for {CurrentTarget}.");
            }
            return;
        }

        public void checkPasswordPrompt(AutoItX3 au, string password, string username, bool usePasswordOnly)
        {
            Log.Trace($"RDP:: Checking for password prompt, usePasswordOnly is  {usePasswordOnly}.");
            bool foundWinSecurity = false; 
            var winHandle = Winuser.FindWindow("Credential Dialog Xaml Host", "Windows Security");
            if (winHandle == IntPtr.Zero)
            {
                //try harder
                foundWinSecurity = true;
                winHandle = findDialogWindow("Windows Security");
                if (winHandle != IntPtr.Zero)
                {
                   Log.Trace($"RDP:: Found window prompt, caption: 'Windows Security' for {CurrentTarget}.");
                }
            } else
            {
                Log.Trace($"RDP:: Found window prompt, caption: 'Credential Dialog Xaml Host' for {CurrentTarget}.");
            }
            if (winHandle != IntPtr.Zero)
            {
                if (CustomLogin != null)
                {
                    Log.Trace($"RDP:: Executing custom login to {CurrentTarget}.");
                    string[] cmds = CustomLogin.Split('\n');
                    foreach ( string cmd in cmds )
                    {
                        if (cmd.Contains("#USERNAME"))
                        {
                            System.Windows.Forms.SendKeys.SendWait(username);
                        }
                        else if (cmd.Contains("#PASSWORD"))
                        {
                           
                            var s = escapePassword(password);
                            System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                        }
                        else if (cmd.Contains("#DELAY"))
                        {
                            var s = cmd.Replace("#DELAY", "");
                            int delay;
                            if (int.TryParse(s, out delay))
                            {
                               Thread.Sleep(delay);
                            }
                            
                        }
                        else
                        {
                            au.Send(cmd);
                        }
                        
                    }
                    Log.Trace($"RDP:: Finished custom login to {CurrentTarget}.");

                }
                else if (usePasswordOnly)
                {
                    string responseString;
                    if (foundWinSecurity)
                    {
                        responseString = "{TAB}{TAB}{ENTER}";  //this form has a different response string
                    } else
                    {
                        responseString = "{TAB}{TAB}{TAB}{ENTER}";
                    }
                    //password prompt is up.  handle it.
                    Winuser.SetForegroundWindow(winHandle);
                    var s = escapePassword(password);
                    System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                    au.Send(responseString);
                    Thread.Sleep(1000);
                    Log.Trace($"RDP:: Found password prompt for {CurrentTarget}.");
                }  else
                {
                    //try entering full username
                    Winuser.SetForegroundWindow(winHandle);
                    if (foundWinSecurity)
                    {
                        Log.Trace($"RDP:: Attempting user/password fill on 'Windows Security' window for {CurrentTarget}.");
                        au.Send("{DOWN}");  //moves down to next user
                        Thread.Sleep(1000);
                        Winuser.SetForegroundWindow(winHandle);
                        System.Windows.Forms.SendKeys.SendWait(username);  //fill in user field, tab to password field
                        au.Send("{TAB}");
                        var s = escapePassword(password);
                        System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                        au.Send("{ENTER}");
                        Thread.Sleep(1000);
                    }
                    else
                    {
                        Log.Trace($"RDP:: Attempting More Choices option for user/password prompt for {CurrentTarget}.");
                        au.Send("{TAB}{TAB}{ENTER}");  //this clicks on 'More Choices'
                        Thread.Sleep(1000);
                        Winuser.SetForegroundWindow(winHandle);
                        au.Send("{TAB}{TAB}{ENTER}");  //this clicks on 'Use diff user'
                        Thread.Sleep(1000);
                        Winuser.SetForegroundWindow(winHandle);
                        System.Windows.Forms.SendKeys.SendWait(username);  //fill in user field, tab to password field
                        au.Send("{TAB}");
                        var s = escapePassword(password);
                        System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                        au.Send("{ENTER}");
                        Thread.Sleep(1000);
                    }
                    Log.Trace($"RDP:: Found user/password prompt for {CurrentTarget}.");

                }


            }
            return;
        }
    }
}
