using System;
using System.Diagnostics;
using System.Threading;
using Ghosts.Client.Infrastructure;
using Ghosts.Domain;
using Ghosts.Domain.Code;
using WorkingHours = Ghosts.Client.Infrastructure.WorkingHours;
using AutoItX3Lib;
using System.Linq;
using System.Text;


namespace Ghosts.Client.Handlers
{
    /// <summary>
    /// Remote Desktop Protocol Handler
    /// </summary>
    public class Rdp : BaseHandler
    {
        private int MouseSleep = 10000;

        public int ExecutionTime = 20000;

        public int executionprobability = 100;
        public int jitterfactor { get; set; } = 0;  //used with Jitter.JitterFactorDelay
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

            if (handler.HandlerArgs.ContainsKey("execution-probability"))
            {
                int.TryParse(handler.HandlerArgs["execution-probability"].ToString(), out executionprobability);
                if (executionprobability < 0 || executionprobability > 100) executionprobability = 100;
            }
            if (handler.HandlerArgs.ContainsKey("delay-jitter"))
            {
                jitterfactor = Jitter.JitterFactorParse(handler.HandlerArgs["delay-jitter"].ToString());
            }
            foreach (var timelineEvent in handler.TimeLineEvents)
            {
                WorkingHours.Is(handler);

                if (timelineEvent.DelayBefore > 0)
                    Thread.Sleep(timelineEvent.DelayBefore);

                switch (timelineEvent.Command)
                {

                    case "random":
                    default:
                        while (true)
                        {
                            if (executionprobability < _random.Next(0, 100))
                            {
                                //skipping this command
                                Log.Trace($"RDP choice skipped due to execution probability");
                                Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor));
                                continue;
                            }
                            var target = timelineEvent.CommandArgs[_random.Next(0, timelineEvent.CommandArgs.Count)];
                            if (!string.IsNullOrEmpty(target.ToString()))
                            {
                                this.RdpEx(handler, timelineEvent, target.ToString());
                            }
                            Thread.Sleep(Jitter.JitterFactorDelay(timelineEvent.DelayAfter, jitterfactor));
                        }
                    
                }

                if (timelineEvent.DelayAfter > 0)
                    Thread.Sleep(timelineEvent.DelayAfter);
            }
        }

        public void RdpEx(TimelineHandler handler, TimelineEvent timelineEvent, string target)
        {
            string command = $"mstsc /v:{target}";
            Log.Trace($"RDP:: Spawning RDP connection for target {target}");
            var processStartInfo = new ProcessStartInfo("cmd.exe")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            string password = "P455w0rd!";
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
                    doMouseLoop(target,au);
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
                } else
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
                    Thread.Sleep(MouseSleep);
                    totalTime += MouseSleep;
                    //move the mouse
                    Winuser.SetForegroundWindow(winHandle);
                    Winuser.RECT rc;
                    Winuser.GetWindowRect(winHandle, out rc);
                    au.MouseMove(rc.Left, rc.Top,30);
                    au.MouseMove(_random.Next(rc.Left, rc.Right), _random.Next(rc.Top, rc.Bottom),30);
                    Winuser.SetForegroundWindow(winHandle);
                }
                //close the window
                Log.Trace($"RDP:: Finished activity loop for {target}.");
                au.WinClose(caption);
                Thread.Sleep(1000);
                // handle the close dialog
                checkGenericPrompt(au, "{ENTER}");
                Thread.Sleep(1000);

            } else
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
            char[] special = { '+', '^', '%', '(', ')', '~', '{' , '}', '[',']'};
            var s = password;
            foreach (var c in special)
            {
               
                var cstr = c.ToString();
                if (s.Contains(cstr))
                {
                    var rep = "{" + cstr + "}";
                    s = s.Replace(cstr,rep);   
                }
                    
            }
            return s;
        }

        public void checkGenericPrompt(AutoItX3 au, string closeString)
        {
            var winHandle = Winuser.FindWindow(null, "Remote Desktop Connection");
            if (winHandle != IntPtr.Zero)
            {
                //this finds any window with title 'Remote Desktop Connection'.
                //determine if class name has 'Dialog' in it
                int bufSize = 256;
                StringBuilder buffer = new StringBuilder(bufSize);
                Winuser.GetClassName(winHandle, buffer, bufSize);
                var s = buffer.ToString();
                if (s.Contains("32770"))
                {
                    //this is a standard dialog class. By default, chooses 'No'. Select yes prompt
                    Winuser.SetForegroundWindow(winHandle);
                    au.Send(closeString); //send the close string
                    Thread.Sleep(1000);
                }


            }
            return;
        }

        public void checkPasswordPrompt(AutoItX3 au, string password)
        {

            var winHandle = Winuser.FindWindow("Credential Dialog Xaml Host", "Windows Security");
            if (winHandle != IntPtr.Zero)
            {
                //password prompt is up.  handle it.
                Winuser.SetForegroundWindow(winHandle);
                var s = escapePassword(password);
                //au.Send(s);
                System.Windows.Forms.SendKeys.SendWait(s);  //fill in password field
                s = "{TAB}{TAB}{TAB}{ENTER}";   //tab to OK button
                au.Send(s);
                Thread.Sleep(1000);
                

            }
            return;
        }
    }
}
