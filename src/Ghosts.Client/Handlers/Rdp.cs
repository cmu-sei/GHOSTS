using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using WorkingHours = Ghosts.Client.Infrastructure.WorkingHours;
using AutoItX3Lib;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Remote Desktop Protocol Handler
    /// Action:  The logged-in user (i.e. ghosts user) opens a remote desktop to randomly chosen target, and does random
    /// mouse movements for the specified ExecutionTime. When this time has elapsed, the connection is closed,
    /// another target is chosen, and the cycle repeats.
    /// 
    /// The Credential file is only used to get a password value, the username is ignored as it is assumed to
    /// be the password of the logged-in user. Even though only a password is needed, the Credential
    /// file is used to be compatible with other handlers that use a credential file (SFTP, SSH, Sharepoint, etc).
    /// 
    /// See the sample Rdp timeline for all options.
    /// 
    /// This handler will supply a password if prompted, and dismiss a untrused certificate check prompt if one
    /// appears.
    /// 
    /// It is assumed that the logged-in user has the right to log into the target system.
    /// Caveat: This has only be tested in a Windows domain system where the domain policy was altered to allow all users
    /// remote desktop priviledges, and only tested using RDP to a domain controller server.
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
                ProcessManager.KillProcessAndChildrenByName(ProcessManager.ProcessNames.Command);
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
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, JitterFactor));
                                continue;
                            }
                            if (timelineEvent.DelayBefore > 0)
                                Thread.Sleep(timelineEvent.DelayBefore);
                            var choice = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(choice.ToString()))
                            {
                                this.RdpEx(handler, timelineEvent, choice.ToString());
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, JitterFactor));
                            break;

                    }
                }
            }
        }

        public void RdpEx(TimelineHandler handler, TimelineEvent timelineEvent, string choice)
        {

            char[] charSeparators = new char[] { '|' };
            var cmdArgs = choice.Split(charSeparators, 2, StringSplitOptions.None);
            var target = cmdArgs[0];
            CurrentTarget = target;
            string command = $"mstsc /v:{target}";
            var credKey = cmdArgs[1];
            var password = CurrentCreds.GetPassword(credKey);
            Log.Trace($"RDP:: Spawning RDP connection for target {target}");
            var processStartInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };


            AutoItX3 au = new AutoItX3();

            using (var process = Process.Start(processStartInfo))
            {
                Thread.Sleep(1000);
                if (process != null)
                {
                    process.StandardInput.WriteLine(command);
                    process.StandardInput.Close(); // line added to stop process from hanging on ReadToEnd()
                    Thread.Sleep(2000);  //give time for response
                    checkPasswordPrompt(au, password);
                    checkGenericPrompt(au, "{TAB}{TAB}{TAB}{ENTER}");  //this is for a certificate prompt
                    Thread.Sleep(10000);  //wait for connection
                    doMouseLoop(target, au);
                    Thread.Sleep(2000);
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.CloseMainWindow();
                            Thread.Sleep(1000);
                        }
                        catch { }

                    }
                    if (!process.HasExited)
                    {
                        try
                        {
                            //ok, stop being nice and just kill it
                            process.Kill();
                            Thread.Sleep(1000);
                        }
                        catch { }
                    }
                }
                else
                {
                    Log.Trace($"RDP:: Failed to execute the remote desktop connection program for {target}");
                }
            }
        }

        public void doMouseLoop(string target, AutoItX3 au)
        {
            var caption = $"{target} - Remote Desktop Connection";
            var winHandle = Winuser.FindWindow("TscShellContainerClass", caption);
            Log.Trace($"RDP:: Connected to {target}, beginning activity loop");
            if (winHandle != IntPtr.Zero)
            {
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

        public void checkPasswordPrompt(AutoItX3 au, string password)
        {

            var winHandle = Winuser.FindWindow("Credential Dialog Xaml Host", "Windows Security");
            var responseString = "{TAB}{TAB}{TAB}{ENTER}";
            if (winHandle == IntPtr.Zero)
            {
                //try harder
                winHandle = findDialogWindow("Windows Security");
                responseString = "{TAB}{TAB}{ENTER}";  //this form has a different response string
            }
            if (winHandle != IntPtr.Zero)
            {
                //password prompt is up.  handle it.
                Winuser.SetForegroundWindow(winHandle);
                var s = escapePassword(password);
                //au.Send(s);
                System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                au.Send(responseString);
                Thread.Sleep(1000);
                Log.Trace($"RDP:: Found password prompt for {CurrentTarget}.");


            }
            return;
        }
    }
}
